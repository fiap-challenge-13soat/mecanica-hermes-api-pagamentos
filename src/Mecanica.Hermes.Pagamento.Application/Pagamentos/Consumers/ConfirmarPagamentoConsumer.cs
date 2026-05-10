using MassTransit;
using Mecanica.Hermes.Contracts.Integration;
using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Constants;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Shared.Application.Messaging;
using Mecanica.Hermes.Shared.Core.Persistence;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers;

/// <summary>
///     Consumer que confirma o pagamento (origem: webhook do Mercado Pago ou polling
///     que retornou <c>approved</c>). Após confirmar localmente, publica
///     <see cref="PagamentoConfirmadoEvent" /> no bus para a SAGA da OS finalizar o ciclo.
/// </summary>
public sealed class ConfirmarPagamentoConsumer(
    IPagamentoRepository repository,
    IUnitOfWork unitOfWork,
    IPagamentoMetrics metrics)
    : PagamentoConsumerBase<ExecuteConfirmarPagamento>
{
    // Default da base — sobrescrito dinamicamente por mensagem via `MetricKeyFor(via)`.
    protected override string OperacaoMetricKey => PagamentoMetricKeys.Operations.ConfirmarWebhook;

    private static string MetricKeyFor(PagamentoConfirmadoVia via) => via switch
    {
        PagamentoConfirmadoVia.Polling => PagamentoMetricKeys.Operations.ConfirmarPolling,
        _ => PagamentoMetricKeys.Operations.ConfirmarWebhook
    };

    public override async Task Consume(ConsumeContext<ExecuteConfirmarPagamento> context)
    {
        var msg = context.Message;
        var metricKey = MetricKeyFor(msg.Via);

        var pagamento = await repository.GetByIdAsync(msg.PagamentoId);
        if (pagamento is null)
        {
            metrics.ErroRegistrado(metricKey, PagamentoMetricKeys.ErrorTypes.NotFound);
            await PublishFailed(context, "Pagamento não encontrado.");
            return;
        }

        // Idempotência: se já está pago, apenas reemite o callback à OS (defensivo) e completa.
        if (pagamento.StatusAtual.StatusAtual == PagamentoStatus.Pago)
        {
            await context.Publish(new PagamentoConfirmadoEvent(
                pagamento.OrdemDeServicoId, pagamento.Id, DateTime.UtcNow));
            await PublishCompleted(context);
            return;
        }

        var confirmar = pagamento.Confirmar(msg.Via);
        if (confirmar.IsFailure)
        {
            metrics.ErroRegistrado(metricKey, PagamentoMetricKeys.ErrorTypes.InvalidStatus);
            await PublishFailed(context, confirmar.Errors);
            return;
        }

        await repository.UpdateAsync(pagamento);

        try
        {
            await unitOfWork.CommitAsync(context.CancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            metrics.ErroRegistrado(metricKey, PagamentoMetricKeys.ErrorTypes.ConcurrencyConflict);
            // Propaga para o MassTransit reprocessar via UseMessageRetry/UseDelayedRedelivery
            // — o swallow anterior contradizia a mensagem e perdia o retry automático.
            throw;
        }

        await context.Publish(new PagamentoConfirmadoEvent(pagamento.OrdemDeServicoId, pagamento.Id, DateTime.UtcNow));

        metrics.PagamentoConfirmado(msg.Via.ToString());

        await PublishCompleted(context);
    }
}
