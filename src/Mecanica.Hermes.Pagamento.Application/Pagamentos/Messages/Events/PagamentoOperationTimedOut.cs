namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Events;

/// <summary>
///     Mensagem agendada pelo <c>DelayedMessageScheduler</c> no momento em que a SAGA entra em
///     estado <c>Processing</c>. Se a operação não concluir dentro do prazo configurado,
///     este evento chegará à SAGA, que forçará a transição de volta para <c>Initial</c>
///     registrando o timeout como motivo de falha.
/// </summary>
public record PagamentoOperationTimedOut(Guid PagamentoId, string Operacao);
