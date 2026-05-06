using MassTransit;
using Mecanica.Hermes.Pagamento.Application.Common.Exceptions;
using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Pagamento.Application.Common.Persistence;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Constants;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers;

public sealed class RecusarPagamentoConsumer(
    IPagamentoRepository repository,
    IUnitOfWork unitOfWork,
    IPagamentoMetrics metrics)
    : PagamentoConsumerBase<ExecuteRecusarPagamento>
{
    protected override string OperacaoMetricKey => PagamentoMetricKeys.Operations.Recusar;

    public override async Task Consume(ConsumeContext<ExecuteRecusarPagamento> context)
    {
        var msg = context.Message;

        var pagamento = await repository.GetByIdAsync(msg.PagamentoId);
        if (pagamento is null)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.NotFound);
            await PublishFailed(context, "Pagamento não encontrado.");
            return;
        }

        var recusar = pagamento.Recusar(msg.Motivo);
        if (recusar.IsFailure)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.InvalidStatus);
            await PublishFailed(context, recusar.Errors);
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

        await context.Publish(new PagamentoRecusadoEvent(
            pagamento.OrdemDeServicoId,
            pagamento.Id,
            msg.Motivo,
            DateTime.UtcNow));

        metrics.PagamentoRecusado();

        await PublishCompleted(context);
    }
}
