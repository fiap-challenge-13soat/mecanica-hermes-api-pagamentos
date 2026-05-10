using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;

/// <summary>
///     Documento Mongo da coleção <c>pagamento_outbox</c>. Cada entrada representa um Domain
///     Event capturado durante um <c>CommitAsync</c>; é processado em background pelo
///     <see cref="Outbox.MongoOutboxProcessor" />.
/// </summary>
internal sealed class OutboxDocument
{
    public const int DefaultMaxRetries = 10;

    public const int StatusPending = 0;
    public const int StatusProcessed = 1;
    public const int StatusDeadLettered = 2;

    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredOn { get; set; }
    public int Status { get; set; }
    public DateTime? ProcessedOn { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public string? TraceContext { get; set; }

    /// <summary>Identificador da instância que reivindicou o documento (TTL fallback).</summary>
    public string? ClaimedBy { get; set; }
    public DateTime? ClaimedAt { get; set; }
}
