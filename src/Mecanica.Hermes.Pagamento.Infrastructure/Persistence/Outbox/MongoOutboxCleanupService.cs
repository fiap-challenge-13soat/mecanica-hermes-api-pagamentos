#pragma warning disable CA1873 // Log arguments are simple property/variable accesses — not expensive
using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Outbox;

/// <summary>
///     Remove periodicamente documentos do Outbox com <c>Status = Processed</c> cuja
///     data de processamento é anterior a <see cref="OutboxOptions.ProcessedRetention" />.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "BackgroundService de limpeza Mongo — coberto por testes de integração com Testcontainers.")]
internal sealed class MongoOutboxCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<MongoOutboxCleanupService> logger) : BackgroundService
{
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cleanupInterval = _options.PollInterval * 10;
        logger.LogInformation("MongoOutboxCleanupService iniciado. Cleanup a cada {Interval}.", cleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                await Task.Delay(cleanupInterval, stoppingToken);
                await CleanupAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                /* shutdown */
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Falha ao executar cleanup do Outbox.");
            }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

        var cutoff = DateTime.UtcNow - _options.ProcessedRetention;

        var filter = Builders<OutboxDocument>.Filter.And(
            Builders<OutboxDocument>.Filter.Eq(d => d.Status, OutboxDocument.StatusProcessed),
            Builders<OutboxDocument>.Filter.Lte(d => d.ProcessedOn, cutoff));

        var result = await db.Outbox.DeleteManyAsync(filter, ct);

        if (result.DeletedCount > 0)
            logger.LogInformation(
                "MongoOutboxCleanupService removeu {Count} documentos processados anteriores a {Cutoff:O}.",
                result.DeletedCount, cutoff);
    }
}
