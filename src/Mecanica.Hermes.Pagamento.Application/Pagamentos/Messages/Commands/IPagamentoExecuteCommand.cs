namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;

/// <summary>
///     Marca um comando despachado pela SAGA para um consumer especializado.
///     O <see cref="PagamentoId" /> é o mesmo CorrelationId da SAGA, o que permite
///     que o consumer publique <c>PagamentoOperationCompleted/Failed</c> de volta para a SAGA.
/// </summary>
public interface IPagamentoExecuteCommand
{
    Guid PagamentoId { get; }
}
