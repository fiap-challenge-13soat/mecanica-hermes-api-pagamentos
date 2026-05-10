using FluentAssertions;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using Mecanica.Hermes.Pagamento.IntegrationTests.Infrastructure;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Mecanica.Hermes.Shared.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Mecanica.Hermes.Pagamento.IntegrationTests.Persistence;

[Collection(IntegrationTestCollection.Name)]
public sealed class OutboxIntegrationTests(IntegrationTestWebApplicationFactory factory)
{
    [Fact]
    public async Task UnitOfWork_CommitAsync_PersisteDomainEventsNaColecaoOutbox()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPagamentoRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var ctx = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

        // Criar pagamento + GerarLink + MarcarEmailEnviado emite múltiplos eventos de domínio.
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        await repo.AddAsync(pagamento);
        await uow.CommitAsync(CancellationToken.None);

        var pendentes = await ctx.Outbox
            .Find(d => d.Status == OutboxDocument.StatusPending)
            .ToListAsync();
        pendentes.Should().NotBeEmpty(
            "CollectEventsFrom + CommitAsync precisa materializar os Domain Events no outbox");
        pendentes.Should().OnlyContain(d => !string.IsNullOrEmpty(d.Type),
            "todo evento serializado precisa ter o nome do tipo (DomainEventNameAttribute ou FullName)");
        pendentes.Should().OnlyContain(d => !string.IsNullOrEmpty(d.Payload),
            "todo evento serializado precisa carregar payload JSON");
    }

    [Fact]
    public async Task MongoOutboxProcessor_DespachaEventosPendentesEMarcaProcessed()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPagamentoRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var ctx = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        await repo.AddAsync(pagamento);
        await uow.CommitAsync(CancellationToken.None);

        // Aguarda o MongoOutboxProcessor (PollInterval=200ms no factory) drenar os pendentes.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long restantes = -1;
        while (sw.Elapsed < TimeSpan.FromSeconds(15))
        {
            restantes = await ctx.Outbox.CountDocumentsAsync(
                Builders<OutboxDocument>.Filter.Eq(d => d.Status, OutboxDocument.StatusPending));
            if (restantes == 0) break;
            await Task.Delay(200);
        }

        restantes.Should().Be(0,
            "o MongoOutboxProcessor deve consumir todos os Pending dentro do timeout configurado");

        var processados = await ctx.Outbox.CountDocumentsAsync(
            Builders<OutboxDocument>.Filter.Eq(d => d.Status, OutboxDocument.StatusProcessed));
        processados.Should().BeGreaterThan(0,
            "documentos despachados precisam ser marcados como Processed (com ProcessedOn preenchido)");
    }
}
