using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;

/// <summary>
///     Documento de idempotência de webhook do Mercado Pago. A chave primária é o id do
///     evento entregue pelo MP (cabeçalho da request) — assim, retransmissões são detectadas
///     por unique constraint na inserção.
/// </summary>
internal sealed class WebhookEventDocument
{
    [BsonId]
    public string MercadoPagoEventId { get; set; } = string.Empty;

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid PagamentoId { get; set; }

    public string Status { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}
