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
///     Unit of Work em MongoDB que materializa os Domain Events coletados dos agregados
///     na coleção <c>pagamento_outbox</c>. O <see cref="Outbox.MongoOutboxProcessor" /> em
///     background despacha cada documento via <see cref="IDomainEventDispatcher" />.
///     <para>
///         <b>Garantia atual:</b> at-least-once via outbox — repositórios e este UoW
///         executam suas escritas sem compartilhar <c>IClientSessionHandle</c>, portanto
///         agregado e outbox NÃO são atômicos hoje. Se a escrita do agregado suceder e
///         o Insert do outbox falhar, os eventos permanecem no buffer interno
///         (<c>_pendingEvents</c>) e podem ser re-tentados em um Commit subsequente.
///         A propagação de sessão (transactional outbox real, com replica set Mongo) é
///         um próximo passo planejado quando a reconciliação por outbox processor não for
///         suficiente.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage(Justification =
    "UoW Mongo com outbox — coberto por testes de integração com Testcontainers.")]
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
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                // JsonSerializer.Serialize lança JsonException (ciclo, profundidade) ou
                // NotSupportedException (tipo sem conversor). Eventos individuais com payload
                // inserializável são logados e o restante segue para o outbox.
                LogSerializationFailure(logger, ex, evt.GetType().Name);
            }

        if (docs.Count == 0)
            return 0;

        await db.Outbox.InsertManyAsync(docs, cancellationToken: cancellationToken);

        // Limpa o buffer só APÓS confirmação da escrita — se InsertManyAsync falhar
        // (rede / cluster transitório), os eventos permanecem no buffer e podem ser
        // re-tentados em um Commit subsequente em vez de serem perdidos silenciosamente.
        _pendingEvents.Clear();
        return docs.Count;
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Falha ao serializar Domain Event {EventType} para Outbox. Evento NÃO será despachado.")]
    private static partial void LogSerializationFailure(ILogger logger, Exception exception, string eventType);
}
