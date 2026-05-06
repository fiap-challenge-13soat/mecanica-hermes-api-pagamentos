using MassTransit;
using Mecanica.Hermes.Pagamento.Application.Common.Exceptions;
using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Pagamento.Application.Common.Persistence;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Constants;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers;

public sealed class ExpirarPagamentoConsumer(
    IPagamentoRepository repository,
    IUnitOfWork unitOfWork,
    IPagamentoMetrics metrics)
    : PagamentoConsumerBase<ExecuteExpirarPagamento>
{
    protected override string OperacaoMetricKey => PagamentoMetricKeys.Operations.Expirar;

    public override async Task Consume(ConsumeContext<ExecuteExpirarPagamento> context)
    {
        var msg = context.Message;

        var pagamento = await repository.GetByIdAsync(msg.PagamentoId);
        if (pagamento is null)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.NotFound);
            await PublishFailed(context, "Pagamento não encontrado.");
            return;
        }

        var expirar = pagamento.Expirar();
        if (expirar.IsFailure)
        {
            metrics.ErroRegistrado(OperacaoMetricKey, PagamentoMetricKeys.ErrorTypes.InvalidStatus);
            await PublishFailed(context, expirar.Errors);
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
            "Pagamento expirado: limite de verificações atingido sem confirmação.",
            DateTime.UtcNow));

        metrics.PagamentoExpirado();

        await PublishCompleted(context);
    }
}
