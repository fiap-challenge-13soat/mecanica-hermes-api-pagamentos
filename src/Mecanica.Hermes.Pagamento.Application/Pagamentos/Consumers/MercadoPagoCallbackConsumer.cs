using MassTransit;
using Mecanica.Hermes.Shared.Core.Persistence;
using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Shared.Application.Messaging;
using Mecanica.Hermes.Shared.Core.Persistence;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Constants;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Contracts.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers;

/// <summary>
///     Consume o <see cref="ExecuteMercadoPagoCallback" /> que a SAGA publica em resposta ao
///     webhook validado. Mapeia o status textual do Mercado Pago para a transição de domínio
///     correspondente (confirmar / recusar). Status <c>pending</c> não causa mutação — o polling
///     agendado tratará a reconciliação.
/// </summary>
public sealed class MercadoPagoCallbackConsumer(
    IPagamentoRepository repository,
    IUnitOfWork unitOfWork,
    IPagamentoMetrics metrics)
    : PagamentoConsumerBase<ExecuteMercadoPagoCallback>
{
    protected override string OperacaoMetricKey => PagamentoMetricKeys.Operations.ConfirmarWebhook;

    public override async Task Consume(ConsumeContext<ExecuteMercadoPagoCallback> context)
    {
        var msg = context.Message;
        var status = (msg.MercadoPagoStatus ?? string.Empty).Trim().ToLowerInvariant();

        var pagamento = await repository.GetByIdAsync(msg.PagamentoId);
        if (pagamento is null)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.NotFound);
            await PublishFailed(context, "Pagamento não encontrado para o callback do Mercado Pago.");
            return;
        }

        switch (status)
        {
            case "approved":
                metrics.WebhookRecebido(PagamentoMetricKeys.WebhookOutcomes.Approved);

                if (pagamento.StatusAtual.StatusAtual == PagamentoStatus.Pago)
                {
                    // Idempotência: webhook duplicado ou polling chegou primeiro.
                    await context.Publish(new PagamentoConfirmadoEvent(pagamento.OrdemDeServicoId, pagamento.Id, DateTime.UtcNow));
                    await PublishCompleted(context);
                    return;
                }

                var confirmar = pagamento.Confirmar(PagamentoConfirmadoVia.Webhook);
                if (confirmar.IsFailure)
                {
                    metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.InvalidStatus);
                    await PublishFailed(context, confirmar.Errors);
                    return;
                }

                if (!await CommitOrFail(context, pagamento)) return;

                await context.Publish(new PagamentoConfirmadoEvent(pagamento.OrdemDeServicoId, pagamento.Id, DateTime.UtcNow));
                metrics.PagamentoConfirmado(PagamentoConfirmadoVia.Webhook.ToString());
                break;

            case "rejected":
            case "cancelled":
                metrics.WebhookRecebido(PagamentoMetricKeys.WebhookOutcomes.Rejected);

                var recusar = pagamento.Recusar($"Mercado Pago retornou status '{status}'.");
                if (recusar.IsFailure)
                {
                    metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.InvalidStatus);
                    await PublishFailed(context, recusar.Errors);
                    return;
                }

                if (!await CommitOrFail(context, pagamento)) return;

                await context.Publish(new PagamentoRecusadoEvent(
                    pagamento.OrdemDeServicoId,
                    pagamento.Id,
                    pagamento.UltimoMotivoFalha ?? "Pagamento recusado pelo Mercado Pago.",
                    DateTime.UtcNow));
                metrics.PagamentoRecusado();
                break;

            case "pending":
            case "in_process":
            case "in_mediation":
                metrics.WebhookRecebido(PagamentoMetricKeys.WebhookOutcomes.Pending);
                // Sem mudança de estado: o polling agendado tratará a reconciliação.
                break;

            default:
                metrics.WebhookRecebido(status);
                break;
        }

        await PublishCompleted(context);
    }

    private async Task<bool> CommitOrFail(
        ConsumeContext<ExecuteMercadoPagoCallback> context,
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
}
