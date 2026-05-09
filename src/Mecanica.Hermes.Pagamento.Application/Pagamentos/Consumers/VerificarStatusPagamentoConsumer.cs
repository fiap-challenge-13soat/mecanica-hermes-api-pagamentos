using System.Diagnostics;
using System.Text.Json;
using MassTransit;
using Mecanica.Hermes.Contracts.Integration;
using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Constants;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Shared.Application.Messaging;
using Mecanica.Hermes.Shared.Core.Persistence;
using Mecanica.Hermes.Shared.Core.Results;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers;

/// <summary>
///     Consumer do polling agendado: consulta o Mercado Pago e decide a próxima ação:
///     <list type="bullet">
///         <item><c>Approved</c> → confirma localmente + publica callback à OS;</item>
///         <item><c>Rejected/Cancelled</c> → recusa + publica integration event de erro;</item>
///         <item><c>Expired</c> ou tentativas esgotadas → expira + publica integration event;</item>
///         <item><c>Pending</c> com tentativas restantes → reagenda nova verificação.</item>
///     </list>
/// </summary>
public sealed class VerificarStatusPagamentoConsumer(
    IPagamentoRepository repository,
    IUnitOfWork unitOfWork,
    IMercadoPagoClient mercadoPagoClient,
    IPagamentoMetrics metrics,
    IPagamentoSchedulerOptions schedulerOptions)
    : PagamentoConsumerBase<ExecuteVerificarStatusPagamento>
{
    protected override string OperacaoMetricKey => PagamentoMetricKeys.Operations.VerificarStatus;

    public override async Task Consume(ConsumeContext<ExecuteVerificarStatusPagamento> context)
    {
        var pagamento = await repository.GetByIdAsync(context.Message.PagamentoId);
        if (pagamento is null)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.NotFound);
            await PublishFailed(context, "Pagamento não encontrado.");
            return;
        }

        if (!ShouldConsultMercadoPago(pagamento))
        {
            await PublishCompleted(context);
            return;
        }

        var outcome = await TryApplyOutcomeAsync(context, pagamento);
        if (outcome.ShouldStop)
            return;

        if (!await TryPersistAsync(context, pagamento))
            return;

        await PublishTransitionEventAsync(context, pagamento, outcome.ConfirmadoVia);
        await PublishCompleted(context);
    }

    private static bool ShouldConsultMercadoPago(Domain.Pagamentos.Pagamento pagamento)
    {
        if (pagamento.StatusAtual.IsTerminal)
            return false;

        var status = pagamento.StatusAtual.StatusAtual;
        return status is PagamentoStatus.AguardandoConfirmacao or PagamentoStatus.LinkGerado;
    }

    private async Task<(bool ShouldStop, PagamentoConfirmadoVia? ConfirmadoVia)> TryApplyOutcomeAsync(
        ConsumeContext<ExecuteVerificarStatusPagamento> context,
        Domain.Pagamentos.Pagamento pagamento)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var status = await mercadoPagoClient.GetPaymentStatusAsync(pagamento.Id, context.CancellationToken);
            return await ApplyOutcomeAsync(context, pagamento, status);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or TimeoutException or JsonException)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.MercadoPagoError);
            await PublishFailed(context, $"Falha ao consultar status no Mercado Pago: {ex.Message}");
            return (true, null);
        }
        finally
        {
            sw.Stop();
            metrics.MercadoPagoConsultaDuracao(sw.Elapsed.TotalMilliseconds);
        }
    }

    private async Task<(bool ShouldStop, PagamentoConfirmadoVia? ConfirmadoVia)> ApplyOutcomeAsync(
        ConsumeContext<ExecuteVerificarStatusPagamento> context,
        Domain.Pagamentos.Pagamento pagamento,
        MercadoPagoPaymentStatus status)
    {
        switch (status.Outcome)
        {
            case MercadoPagoPaymentOutcome.Approved:
                return await ApplyTransitionAsync(
                    context, pagamento.Confirmar(PagamentoConfirmadoVia.Polling), PagamentoConfirmadoVia.Polling);

            case MercadoPagoPaymentOutcome.Rejected:
            case MercadoPagoPaymentOutcome.Cancelled:
                return await ApplyTransitionAsync(context, pagamento.Recusar(status.DetalheOriginal));

            case MercadoPagoPaymentOutcome.Expired:
                return await ApplyTransitionAsync(context, pagamento.Expirar());

            case MercadoPagoPaymentOutcome.Pending:
            case MercadoPagoPaymentOutcome.NotFound:
                return await ApplyPendingAsync(context, pagamento);

            default:
                return (false, null);
        }
    }

    private async Task<(bool ShouldStop, PagamentoConfirmadoVia? ConfirmadoVia)> ApplyTransitionAsync(
        ConsumeContext<ExecuteVerificarStatusPagamento> context,
        Result transition,
        PagamentoConfirmadoVia? confirmadoVia = null)
    {
        if (transition.IsFailure)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.InvalidStatus);
            await PublishFailed(context, transition.Errors);
            return (true, null);
        }

        return (false, confirmadoVia);
    }

    private async Task<(bool ShouldStop, PagamentoConfirmadoVia? ConfirmadoVia)> ApplyPendingAsync(
        ConsumeContext<ExecuteVerificarStatusPagamento> context,
        Domain.Pagamentos.Pagamento pagamento)
    {
        var tentativa = pagamento.RegistrarTentativaVerificacao();
        if (tentativa.IsFailure)
        {
            await PublishCompleted(context);
            return (true, null);
        }

        if (!pagamento.PodeReverificar)
            return await ApplyTransitionAsync(context, pagamento.Expirar());

        await context.SchedulePublish(
            schedulerOptions.PollingInterval,
            new VerificarStatusPagamentoRequest(pagamento.Id));
        return (false, null);
    }

    private async Task<bool> TryPersistAsync(
        ConsumeContext<ExecuteVerificarStatusPagamento> context,
        Domain.Pagamentos.Pagamento pagamento)
    {
        await repository.UpdateAsync(pagamento);

        try
        {
            await unitOfWork.CommitAsync(context.CancellationToken);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.ConcurrencyConflict);
            await PublishFailed(context, "Conflito de concorrência. A operação será reprocessada.");
            return false;
        }
    }

    private async Task PublishTransitionEventAsync(
        ConsumeContext<ExecuteVerificarStatusPagamento> context,
        Domain.Pagamentos.Pagamento pagamento,
        PagamentoConfirmadoVia? confirmadoVia)
    {
        switch (pagamento.StatusAtual.StatusAtual)
        {
            case PagamentoStatus.Pago:
                await context.Publish(new PagamentoConfirmadoEvent(
                    pagamento.OrdemDeServicoId, pagamento.Id, DateTime.UtcNow));
                metrics.PagamentoConfirmado((confirmadoVia ?? PagamentoConfirmadoVia.Polling).ToString());
                break;

            case PagamentoStatus.Recusado:
                await context.Publish(new PagamentoRecusadoEvent(
                    pagamento.OrdemDeServicoId,
                    pagamento.Id,
                    pagamento.UltimoMotivoFalha ?? "Pagamento recusado pelo Mercado Pago.",
                    DateTime.UtcNow));
                metrics.PagamentoRecusado();
                break;

            case PagamentoStatus.Expirado:
                await context.Publish(new PagamentoRecusadoEvent(
                    pagamento.OrdemDeServicoId,
                    pagamento.Id,
                    "Pagamento expirado.",
                    DateTime.UtcNow));
                metrics.PagamentoExpirado();
                break;
        }
    }
}
