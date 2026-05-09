using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Infrastructure.Observability;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using Mecanica.Hermes.Shared.Core.Domain;
using Mecanica.Hermes.Shared.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Outbox;

/// <summary>
///     Background service que processa documentos pendentes da coleção
///     <c>pagamento_outbox</c>. Estratégia de concorrência:
///     <list type="bullet">
///         <item>
///             <b>Claim atômico:</b> usa <c>FindOneAndUpdate</c> em loop para reivindicar até
///             <c>BatchSize</c> documentos com <c>Status = Pending</c>, marcando-os com
///             <c>ClaimedBy</c>+<c>ClaimedAt</c>. Documentos reivindicados há mais de
///             <see cref="OutboxOptions.ClaimTtl" /> são considerados órfãos e podem ser
///             reivindicados por outra instância.
///         </item>
///         <item>
///             <b>Despacho:</b> deserializa o payload, dispara via
///             <see cref="IDomainEventDispatcher" /> e marca <c>Processed</c>.
///         </item>
///         <item>
///             <b>DLQ:</b> após <see cref="OutboxOptions.MaxRetries" />, o documento é movido
///             para <c>DeadLettered</c>.
///         </item>
///     </list>
/// </summary>
[ExcludeFromCodeCoverage(Justification = "BackgroundService que faz I/O Mongo — coberto por testes de integração com Testcontainers.")]
internal sealed partial class MongoOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    OutboxHealthMetrics metrics,
    IOptions<OutboxOptions> options,
    ILogger<MongoOutboxProcessor> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogIniciado(logger, _instanceId, _options.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogFalhaLote(logger, ex, _options.PollInterval);
            }

            try
            {
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                /* shutdown */
            }
        }

        LogParado(logger);
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var staleClaim = now - _options.ClaimTtl;

        // Reivindica até BatchSize documentos disponíveis (pendentes ou claims expirados).
        var claimed = new List<OutboxDocument>(_options.BatchSize);
        for (var i = 0; i < _options.BatchSize; i++)
        {
            var filter = Builders<OutboxDocument>.Filter.And(
                Builders<OutboxDocument>.Filter.Eq(d => d.Status, OutboxDocument.StatusPending),
                Builders<OutboxDocument>.Filter.Or(
                    Builders<OutboxDocument>.Filter.Eq(d => d.ClaimedBy, null),
                    Builders<OutboxDocument>.Filter.Lt(d => d.ClaimedAt, staleClaim)));

            var update = Builders<OutboxDocument>.Update
                .Set(d => d.ClaimedBy, _instanceId)
                .Set(d => d.ClaimedAt, now);

            var sort = Builders<OutboxDocument>.Sort.Ascending(d => d.OccurredOn);
            var doc = await db.Outbox.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<OutboxDocument>
                {
                    Sort = sort,
                    ReturnDocument = ReturnDocument.After
                },
                ct);

            if (doc is null) break;
            claimed.Add(doc);
        }

        if (claimed.Count == 0) return;

        foreach (var doc in claimed)
        {
            using var activity = ActivitySources.Api.StartActivity(
                $"outbox.dispatch/{doc.Type}",
                ActivityKind.Internal,
                doc.TraceContext ?? string.Empty);

            activity?.SetTag("outbox.message_id", doc.Id.ToString());
            activity?.SetTag("outbox.event_type", doc.Type);
            activity?.SetTag("outbox.retry_count", doc.RetryCount);

            try
            {
                var evtType = ResolveEventType(doc.Type);
                if (evtType is null)
                {
                    var err = $"Tipo '{doc.Type}' não pôde ser resolvido.";
                    LogDocumentoInvalido(logger, doc.Id, err);
                    await MarkFailedAsync(db, doc, err, ct);
                    RecordFailure(doc, activity);
                    continue;
                }

                var domainEvent = JsonSerializer.Deserialize(doc.Payload, evtType) as IDomainEvent;
                if (domainEvent is null)
                {
                    const string err = "Payload deserializado é null ou não implementa IDomainEvent.";
                    LogDocumentoInvalido(logger, doc.Id, err);
                    await MarkFailedAsync(db, doc, err, ct);
                    RecordFailure(doc, activity);
                    continue;
                }

                await dispatcher.DispatchAsync([domainEvent], ct);
                await MarkProcessedAsync(db, doc, ct);
                metrics.MessageDispatched();
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Cancelamento do host deve abortar o batch — não marcar o doc como falho
                // (será reprocessado na próxima rodada quando o claim TTL expirar).
                throw;
            }
            catch (Exception ex)
            {
                // Fronteira de erro intencional por documento: qualquer falha (deserialização,
                // dispatch, I/O Mongo) é registrada no doc com retry++ e o batch segue
                // (Sonar S2221 dispensado).
                LogFalhaDispatch(logger, ex, doc.Id, doc.Type, doc.RetryCount + 1);
                await MarkFailedAsync(db, doc, ex.Message, ct);
                RecordFailure(doc, activity);
            }
        }
    }

    private async Task MarkProcessedAsync(MongoDbContext db, OutboxDocument doc, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var update = Builders<OutboxDocument>.Update
            .Set(d => d.Status, OutboxDocument.StatusProcessed)
            .Set(d => d.ProcessedOn, now)
            .Unset(d => d.ClaimedBy)
            .Unset(d => d.ClaimedAt);
        await db.Outbox.UpdateOneAsync(d => d.Id == doc.Id, update, cancellationToken: ct);
    }

    private async Task MarkFailedAsync(MongoDbContext db, OutboxDocument doc, string error, CancellationToken ct)
    {
        var nextRetry = doc.RetryCount + 1;
        var nextStatus = nextRetry >= _options.MaxRetries
            ? OutboxDocument.StatusDeadLettered
            : OutboxDocument.StatusPending;

        var update = Builders<OutboxDocument>.Update
            .Set(d => d.RetryCount, nextRetry)
            .Set(d => d.LastError, error)
            .Set(d => d.Status, nextStatus)
            .Unset(d => d.ClaimedBy)
            .Unset(d => d.ClaimedAt);

        await db.Outbox.UpdateOneAsync(d => d.Id == doc.Id, update, cancellationToken: ct);

        // Atualiza o doc local para o RecordFailure detectar dead-letter.
        doc.RetryCount = nextRetry;
        doc.Status = nextStatus;
    }

    private void RecordFailure(OutboxDocument doc, Activity? activity)
    {
        activity?.SetStatus(ActivityStatusCode.Error, doc.LastError);
        if (doc.Status == OutboxDocument.StatusDeadLettered)
        {
            metrics.MessageDeadLettered();
            LogDeadLetter(logger, doc.Id, doc.Type, _options.MaxRetries);
        }
        else
        {
            metrics.MessageFailed();
        }
    }

    private static Type? ResolveEventType(string typeName)
    {
        var resolved = Type.GetType(typeName, false);
        if (resolved is not null) return resolved;

        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // Assemblies com tipos parcialmente carregáveis (dinâmicos / dependências
                    // ausentes) — usa apenas os tipos que conseguiram resolver.
                    return ex.Types.Where(t => t is not null).ToArray()!;
                }
            })
            .FirstOrDefault(t =>
            {
                var attr = t.GetCustomAttributes(typeof(DomainEventNameAttribute), false)
                    .OfType<DomainEventNameAttribute>()
                    .FirstOrDefault();
                return attr?.Name == typeName;
            });
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "MongoOutboxProcessor iniciado (instance={Instance}). Polling a cada {Interval}.")]
    private static partial void LogIniciado(ILogger logger, string instance, TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Information, Message = "MongoOutboxProcessor parado.")]
    private static partial void LogParado(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Falha ao processar lote do Outbox. Próxima tentativa em {Interval}.")]
    private static partial void LogFalhaLote(ILogger logger, Exception exception, TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OutboxDocument {Id}: {Error}")]
    private static partial void LogDocumentoInvalido(ILogger logger, Guid id, string error);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Falha ao despachar OutboxDocument {Id} ({Type}). Retry={Retry}")]
    private static partial void LogFalhaDispatch(
        ILogger logger,
        Exception exception,
        Guid id,
        string type,
        int retry);

    [LoggerMessage(
        Level = LogLevel.Critical,
        Message = "OutboxDocument {Id} ({Type}) atingiu o limite de {MaxRetries} tentativas e foi movido "
                  + "para Dead Letter.")]
    private static partial void LogDeadLetter(ILogger logger, Guid id, string type, int maxRetries);
}
