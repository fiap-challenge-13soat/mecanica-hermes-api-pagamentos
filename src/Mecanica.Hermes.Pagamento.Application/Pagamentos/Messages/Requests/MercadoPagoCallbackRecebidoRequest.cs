namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;

/// <summary>
///     Publicado pelo endpoint de webhook após validar HMAC e idempotência. A SAGA
///     reage publicando <c>ExecuteConfirmarPagamento</c> no consumer apropriado.
/// </summary>
public sealed record MercadoPagoCallbackRecebidoRequest(
    Guid PagamentoId,
    string MercadoPagoEventId,
    string MercadoPagoStatus) : IPagamentoRequest;
