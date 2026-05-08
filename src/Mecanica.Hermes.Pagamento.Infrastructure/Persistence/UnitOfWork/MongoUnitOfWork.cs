using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using Mecanica.Hermes.Shared.Core.Domain;
using Mecanica.Hermes.Shared.Core.Persistence;
using Microsoft.Extensions.Logging;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.UnitOfWork;

/// <summary>
///     Unit of Work em MongoDB com Outbox transacional. Coleta os Domain Events emitidos
///     pelos agregados e enfileira-os no <c>pagamento_outbox</c> dentro do mesmo escopo de
///     escrita do agregado. O <see cref="Outbox.MongoOutboxProcessor" /> em background
///     despacha cada documento via <see cref="IDomainEventDispatcher" />.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "UoW Mongo com transação + outbox — coberto por testes de integração com Testcontainers.")]
internal sealed partial class MongoUnitOfWork(
    MongoDbContext db,
    TimeProvider timeProvider,
    ILogger<MongoUnitOfWork> logger) : IUnitOfWork
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _pendingEvents = [];

    public void CollectEventsFrom(AggregateRoot aggregate)
    {
        _pendingEvents.AddRange(aggregate.DomainEvents);
        aggregate.ClearDomainEvents();
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken)
    {
        if (_pendingEvents.Count == 0)
            return 0;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var snapshot = _pendingEvents.ToArray();
        _pendingEvents.Clear();

        var docs = new List<OutboxDocument>(snapshot.Length);
        foreach (var evt in snapshot)
            try
            {
                var payload = JsonSerializer.Serialize(evt, evt.GetType(), SerializerOptions);
                var evtType = evt.GetType();
                var nameAttr = (DomainEventNameAttribute?)Attribute.GetCustomAttribute(
                    evtType,
                    typeof(DomainEventNameAttribute));
                var typeName = nameAttr?.Name ?? evtType.AssemblyQualifiedName ?? evtType.FullName!;
                var traceContext = Activity.Current?.Id;

                docs.Add(new OutboxDocument
                {
                    Id = Guid.NewGuid(),
                    Type = typeName,
                    Payload = payload,
                    OccurredOn = now,
                    Status = OutboxDocument.StatusPending,
                    TraceContext = traceContext
                });
            }
            catch (Exception ex)
            {
                LogSerializationFailure(logger, ex, evt.GetType().Name);
            }

        if (docs.Count == 0)
            return 0;

        await db.Outbox.InsertManyAsync(docs, cancellationToken: cancellationToken);
        return docs.Count;
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Falha ao serializar Domain Event {EventType} para Outbox. Evento NÃO será despachado.")]
    private static partial void LogSerializationFailure(ILogger logger, Exception exception, string eventType);
}
