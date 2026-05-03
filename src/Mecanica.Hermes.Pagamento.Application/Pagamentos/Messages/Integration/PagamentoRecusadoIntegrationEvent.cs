namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;

/// <summary>
///     Evento publicado quando um pagamento é recusado ou expira. Será consumido pela
///     futura API de clientes (notificação ao cliente final) — fora do escopo desta API,
///     basta acumular na fila de erro.
/// </summary>
public sealed record PagamentoRecusadoIntegrationEvent(
    Guid PagamentoId,
    Guid OrdemDeServicoId,
    Guid ClienteId,
    string Motivo,
    int Tentativas);
