using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using Mecanica.Hermes.Shared.Core.Domain;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.UnitOfWork;

/// <summary>
///     Unit of Work em MongoDB com <b>outbox transacional</b>: as escritas do agregado e do
///     <c>pagamento_outbox</c> compartilham a mesma <see cref="IClientSessionHandle" /> e são
///     comitadas como uma única transaction. Se o commit do outbox falhar, a transaction é
///     abortada e o agregado também é revertido — garantindo atomicidade ponta a ponta
///     (exige replica set Mongo).
///     <para>
///         Ciclo: o UoW é registrado como <c>Scoped</c>; a sessão é aberta lazily na
///         primeira escrita (via <see cref="IMongoUnitOfWork.GetOrStartSessionAsync" />),
///         persiste durante todo o escopo (uma request HTTP / um Consume) e é comitada/abortada
///         no <see cref="CommitAsync" /> ou <see cref="DisposeAsync" />.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage(Justification =
    "UoW Mongo com transação + outbox — coberto por testes de integração com Testcontainers.")]
internal sealed partial class MongoUnitOfWork(
    MongoDbContext db,
    TimeProvider timeProvider,
    ILogger<MongoUnitOfWork> logger) : IMongoUnitOfWork, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _pendingEvents = [];
    private IClientSessionHandle? _session;
    private bool _transactionStarted;

    public void CollectEventsFrom(AggregateRoot aggregate)
    {
        _pendingEvents.AddRange(aggregate.DomainEvents);
        aggregate.ClearDomainEvents();
    }

    public async Task<IClientSessionHandle> GetOrStartSessionAsync(CancellationToken cancellationToken)
    {
        _session ??= await db.StartSessionAsync(cancellationToken);
        if (!_transactionStarted)
        {
            _session.StartTransaction();
            _transactionStarted = true;
        }
        return _session;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken)
    {
        // Sem eventos E sem transação: nada a fazer (caller fez só leituras).
        if (_pendingEvents.Count == 0 && !_transactionStarted)
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

        try
        {
            if (docs.Count > 0)
            {
                if (_session is not null)
                    await db.Outbox.InsertManyAsync(_session, docs, cancellationToken: cancellationToken);
                else
                    await db.Outbox.InsertManyAsync(docs, cancellationToken: cancellationToken);
            }

            if (_transactionStarted && _session is not null)
            {
                await _session.CommitTransactionAsync(cancellationToken);
                _transactionStarted = false;
            }

            // Limpa o buffer só APÓS confirmação — falha de Insert/Commit preserva os eventos
            // para retry no próximo CommitAsync (ou são descartados pelo dispose após abort).
            _pendingEvents.Clear();
            return docs.Count;
        }
        catch
        {
            await SafeAbortAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await SafeAbortAsync();
        _session?.Dispose();
        _session = null;
    }

    private async Task SafeAbortAsync()
    {
        if (!_transactionStarted || _session is null) return;
        try
        {
            await _session.AbortTransactionAsync();
        }
        catch (Exception ex) when (ex is MongoException or InvalidOperationException)
        {
            // Best-effort abort — sessão já pode estar morta. Log e segue (Dispose vai liberar).
            LogAbortFailure(logger, ex);
        }
        finally
        {
            _transactionStarted = false;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Falha ao serializar Domain Event {EventType} para Outbox. Evento NÃO será despachado.")]
    private static partial void LogSerializationFailure(ILogger logger, Exception exception, string eventType);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao abortar transação Mongo no dispose do UoW (best-effort).")]
    private static partial void LogAbortFailure(ILogger logger, Exception exception);
}
