using MassTransit;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Events;

namespace Mecanica.Hermes.Pagamento.Application.Common.Messaging;

/// <summary>
///     Base abstrata para consumers de comandos da SAGA <c>PagamentoStateMachine</c>.
///     Centraliza o boilerplate de publicação dos eventos
///     <see cref="PagamentoOperationCompleted" /> e <see cref="PagamentoOperationFailed" /> e
///     mantém o nome canônico da operação (usado também em métricas).
/// </summary>
public abstract class PagamentoConsumerBase<TCommand> : IConsumer<TCommand>
    where TCommand : class, IPagamentoExecuteCommand
{
    /// <summary>
    ///     Nome da operação publicado no evento <see cref="PagamentoOperationCompleted" /> /
    ///     <see cref="PagamentoOperationFailed" />. Faz parte do contrato com a SAGA.
    /// </summary>
    protected string OperacaoName { get; } = typeof(TCommand).Name;

    /// <summary>
    ///     Chave de operação usada nas métricas. Override quando quiser uma chave em snake_case
    ///     distinta do nome do tipo.
    /// </summary>
    protected virtual string OperacaoMetricKey => OperacaoName;

    public abstract Task Consume(ConsumeContext<TCommand> context);

    protected Task PublishCompleted(ConsumeContext<TCommand> context)
    {
        return context.Publish(new PagamentoOperationCompleted(context.Message.PagamentoId, OperacaoName));
    }

    protected Task PublishFailed(ConsumeContext<TCommand> context, IEnumerable<string> erros)
    {
        return context.Publish(new PagamentoOperationFailed(
            context.Message.PagamentoId,
            OperacaoName,
            string.Join("; ", erros)));
    }

    protected Task PublishFailed(ConsumeContext<TCommand> context, string motivo)
    {
        return context.Publish(new PagamentoOperationFailed(
            context.Message.PagamentoId,
            OperacaoName,
            motivo));
    }
}
