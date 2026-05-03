using MassTransit;
using Mecanica.Hermes.Application.OrdensDeServico.Messages.Requests;
using Mecanica.Hermes.Pagamento.Application.Common.Exceptions;
using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Pagamento.Application.Common.Persistence;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Constants;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers;

/// <summary>
///     Consumer que confirma o pagamento (origem: webhook do Mercado Pago ou polling
///     que retornou <c>approved</c>). Após confirmar localmente, publica
///     <see cref="PagamentoAprovadoRequest" /> no bus para a SAGA da OS finalizar o ciclo.
/// </summary>
public sealed class ConfirmarPagamentoConsumer(
    IPagamentoRepository repository,
    IUnitOfWork unitOfWork,
    IPagamentoMetrics metrics)
    : PagamentoConsumerBase<ExecuteConfirmarPagamento>
{
    protected override string OperacaoMetricKey => PagamentoMetricKeys.Operations.ConfirmarWebhook;

    public override async Task Consume(ConsumeContext<ExecuteConfirmarPagamento> context)
    {
        var msg = context.Message;

        var pagamento = await repository.GetByIdAsync(msg.PagamentoId);
        if (pagamento is null)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.NotFound);
            await PublishFailed(context, "Pagamento não encontrado.");
            return;
        }

        // Idempotência: se já está pago, apenas reemite o callback à OS (defensivo) e completa.
        if (pagamento.StatusAtual.StatusAtual == PagamentoStatus.Pago)
        {
            await context.Publish(new PagamentoAprovadoRequest(pagamento.OrdemDeServicoId));
            await PublishCompleted(context);
            return;
        }

        var confirmar = pagamento.Confirmar(msg.Via);
        if (confirmar.IsFailure)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.InvalidStatus);
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
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.ConcurrencyConflict);
            await PublishFailed(context, "Conflito de concorrência. A operação será reprocessada.");
            return;
        }

        await context.Publish(new PagamentoAprovadoRequest(pagamento.OrdemDeServicoId));

        metrics.PagamentoConfirmado(msg.Via.ToString());

        await PublishCompleted(context);
    }
}
