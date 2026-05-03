using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence;

/// <summary>
///     Cria os índices necessários nas coleções do MongoDB no startup. Substitui as
///     "migrations" do mundo relacional. Operação idempotente — o driver pula índices
///     pré-existentes com a mesma definição.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Hosted service de bootstrap de índices Mongo — coberto por testes de integração.")]
internal sealed class MongoIndexInitializer(MongoDbContext db, ILogger<MongoIndexInitializer> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Garantindo índices das coleções MongoDB...");

        // pagamentos: índice em OrdemDeServicoId para consulta por OS
        await db.Pagamentos.Indexes.CreateOneAsync(
            new CreateIndexModel<PagamentoDocument>(
                Builders<PagamentoDocument>.IndexKeys.Ascending(d => d.OrdemDeServicoId),
                new CreateIndexOptions { Name = "ix_pagamentos_ordem_de_servico" }),
            cancellationToken: cancellationToken);

        // pagamentos: índice em StatusAtual para filtros / dashboards
        await db.Pagamentos.Indexes.CreateOneAsync(
            new CreateIndexModel<PagamentoDocument>(
                Builders<PagamentoDocument>.IndexKeys.Ascending(d => d.StatusAtual),
                new CreateIndexOptions { Name = "ix_pagamentos_status_atual" }),
            cancellationToken: cancellationToken);

        // pagamento_outbox: índice composto (Status, OccurredOn) para o claim do processor
        await db.Outbox.Indexes.CreateOneAsync(
            new CreateIndexModel<OutboxDocument>(
                Builders<OutboxDocument>.IndexKeys
                    .Ascending(d => d.Status)
                    .Ascending(d => d.OccurredOn),
                new CreateIndexOptions { Name = "ix_outbox_status_occurredon" }),
            cancellationToken: cancellationToken);

        // webhook_events: TTL index para auto-limpeza após 90 dias
        await db.WebhookEvents.Indexes.CreateOneAsync(
            new CreateIndexModel<WebhookEventDocument>(
                Builders<WebhookEventDocument>.IndexKeys.Ascending(d => d.ReceivedAt),
                new CreateIndexOptions { Name = "ix_webhook_received_ttl", ExpireAfter = TimeSpan.FromDays(90) }),
            cancellationToken: cancellationToken);

        logger.LogInformation("Índices das coleções MongoDB confirmados.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
