namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Events;

/// <summary>
///     Publicado por um consumer quando a operação falha (regra de negócio violada,
///     pagamento inexistente, etc). A SAGA volta para <c>Initial</c>, registrando a falha.
/// </summary>
public sealed record PagamentoOperationFailed(
    Guid PagamentoId,
    string Operacao,
    string Motivo);
