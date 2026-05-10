namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

/// <summary>
///     Port (Hexagonal) usado pelo endpoint de webhook para garantir idempotência.
///     Na coleção <c>webhook_events</c>, o id do evento Mercado Pago é a própria chave
///     primária (<c>_id</c>, mapeado por <c>[BsonId]</c> em <c>WebhookEventDocument</c>).
///     A primeira escrita persiste o evento; retransmissões são rejeitadas pela unique
///     constraint nativa do <c>_id</c> (E11000 → traduzido para <c>false</c> no retorno).
/// </summary>
public interface IWebhookEventRepository
{
    /// <summary>
    ///     Tenta registrar o evento. Retorna <c>true</c> se foi inserido (primeira ocorrência),
    ///     ou <c>false</c> se o <paramref name="mercadoPagoEventId" /> já existir como <c>_id</c>
    ///     (duplicate key).
    /// </summary>
    Task<bool> TryRegisterAsync(
        string mercadoPagoEventId,
        Guid pagamentoId,
        string status,
        CancellationToken cancellationToken);
}
