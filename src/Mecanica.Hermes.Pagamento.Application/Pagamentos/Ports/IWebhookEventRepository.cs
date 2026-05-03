namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

/// <summary>
///     Port (Hexagonal) usado pelo endpoint de webhook para garantir idempotência.
///     A coleção <c>webhook_events</c> tem unique index em <c>mp_event_id</c>; a primeira
///     escrita persiste o evento, escritas subsequentes são detectadas como duplicadas e
///     o processamento é descartado em silêncio.
/// </summary>
public interface IWebhookEventRepository
{
    /// <summary>
    ///     Tenta registrar o evento. Retorna <c>true</c> se foi inserido (primeira ocorrência),
    ///     ou <c>false</c> se a chave <c>mercadoPagoEventId</c> já existir (duplicado).
    /// </summary>
    Task<bool> TryRegisterAsync(
        string mercadoPagoEventId,
        Guid pagamentoId,
        string status,
        CancellationToken cancellationToken);
}
