using System.Diagnostics;
using MassTransit;
using Mecanica.Hermes.Application.OrdensDeServico.Messages.Requests;
using Mecanica.Hermes.Pagamento.Application.Common.Exceptions;
using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Pagamento.Application.Common.Persistence;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Constants;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

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
        var msg = context.Message;

        var pagamento = await repository.GetByIdAsync(msg.PagamentoId);
        if (pagamento is null)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.NotFound);
            await PublishFailed(context, "Pagamento não encontrado.");
            return;
        }

        // Estados terminais: nada a fazer.
        if (pagamento.StatusAtual.IsTerminal)
        {
            await PublishCompleted(context);
            return;
        }

        // Só faz sentido consultar quando o link já foi gerado.
        if (pagamento.StatusAtual.StatusAtual != PagamentoStatus.AguardandoConfirmacao &&
            pagamento.StatusAtual.StatusAtual != PagamentoStatus.LinkGerado)
        {
            await PublishCompleted(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        Domain.Pagamentos.Enums.PagamentoConfirmadoVia? confirmadoVia = null;
        try
        {
            var status = await mercadoPagoClient.GetPaymentStatusAsync(pagamento.Id, context.CancellationToken);

            switch (status.Outcome)
            {
                case MercadoPagoPaymentOutcome.Approved:
                    var confirm = pagamento.Confirmar(PagamentoConfirmadoVia.Polling);
                    if (confirm.IsFailure)
                    {
                        metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.InvalidStatus);
                        await PublishFailed(context, confirm.Errors);
                        return;
                    }
                    confirmadoVia = PagamentoConfirmadoVia.Polling;
                    break;

                case MercadoPagoPaymentOutcome.Rejected:
                case MercadoPagoPaymentOutcome.Cancelled:
                    var recusar = pagamento.Recusar(status.DetalheOriginal);
                    if (recusar.IsFailure)
                    {
                        metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.InvalidStatus);
                        await PublishFailed(context, recusar.Errors);
                        return;
                    }
                    break;

                case MercadoPagoPaymentOutcome.Expired:
                    var expirar = pagamento.Expirar();
                    if (expirar.IsFailure)
                    {
                        metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.InvalidStatus);
                        await PublishFailed(context, expirar.Errors);
                        return;
                    }
                    break;

                case MercadoPagoPaymentOutcome.Pending:
                case MercadoPagoPaymentOutcome.NotFound:
                    var tentativa = pagamento.RegistrarTentativaVerificacao();
                    if (tentativa.IsFailure)
                    {
                        await PublishCompleted(context);
                        return;
                    }

                    if (!pagamento.PodeReverificar)
                    {
                        // Sem mais tentativas: expira o pagamento.
                        var exp = pagamento.Expirar();
                        if (exp.IsFailure)
                        {
                            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.InvalidStatus);
                            await PublishFailed(context, exp.Errors);
                            return;
                        }
                    }
                    else
                    {
                        // Reagenda outra verificação no intervalo configurado.
                        await context.SchedulePublish(
                            schedulerOptions.PollingInterval,
                            new VerificarStatusPagamentoRequest(pagamento.Id));
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.MercadoPagoError);
            await PublishFailed(context, $"Falha ao consultar status no Mercado Pago: {ex.Message}");
            return;
        }
        finally
        {
            sw.Stop();
            metrics.MercadoPagoConsultaDuracao(sw.Elapsed.TotalMilliseconds);
        }

        await repository.UpdateAsync(pagamento);

        try
        {
            await unitOfWork.CommitAsync(context.CancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.ConcurrencyConflict);
            await PublishFailed(context, "Conflito de concorrência. A operação será reprocessada.");
            return;
        }

        // Publicações cross-service derivadas da transição já aplicada.
        if (pagamento.StatusAtual.StatusAtual == PagamentoStatus.Pago)
        {
            await context.Publish(new PagamentoAprovadoRequest(pagamento.OrdemDeServicoId));
            metrics.PagamentoConfirmado((confirmadoVia ?? PagamentoConfirmadoVia.Polling).ToString());
        }
        else if (pagamento.StatusAtual.StatusAtual == PagamentoStatus.Recusado)
        {
            await context.Publish(new PagamentoRecusadoIntegrationEvent(
                pagamento.Id,
                pagamento.OrdemDeServicoId,
                pagamento.ClienteId,
                pagamento.UltimoMotivoFalha ?? "Pagamento recusado pelo Mercado Pago.",
                pagamento.TentativasVerificacao));
            metrics.PagamentoRecusado();
        }
        else if (pagamento.StatusAtual.StatusAtual == PagamentoStatus.Expirado)
        {
            await context.Publish(new PagamentoRecusadoIntegrationEvent(
                pagamento.Id,
                pagamento.OrdemDeServicoId,
                pagamento.ClienteId,
                "Pagamento expirado.",
                pagamento.TentativasVerificacao));
            metrics.PagamentoExpirado();
        }

        await PublishCompleted(context);
    }
}
