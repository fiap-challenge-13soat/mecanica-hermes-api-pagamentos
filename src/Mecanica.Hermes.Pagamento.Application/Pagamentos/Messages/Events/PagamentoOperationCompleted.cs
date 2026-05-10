namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Events;

/// <summary>
///     Publicado por um consumer após processar com sucesso uma operação solicitada pela SAGA.
///     Faz a SAGA voltar do estado <c>Processing</c> para <c>Initial</c> ou para
///     <c>AguardandoConfirmacao</c>, conforme a operação concluída.
/// </summary>
public sealed record PagamentoOperationCompleted(
    Guid PagamentoId,
    string Operacao);
