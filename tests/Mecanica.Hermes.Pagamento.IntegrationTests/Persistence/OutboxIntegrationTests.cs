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
    public async Task TransactionalOutbox_SemCommit_NaoPersisteAgregadoNemEventos()
    {
        // Garante que `AddAsync` está REALMENTE dentro de uma transação Mongo: sem
        // `CommitAsync` o agregado e os eventos não devem ser visíveis fora do escopo.
        await factory.ResetDatabaseAsync();
        var pagamentoId = Guid.NewGuid();

        // Escopo isolado — o UoW será disposto sem CommitAsync, abortando a transação.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPagamentoRepository>();

            var pagamento = new PagamentoBuilder().WithId(pagamentoId).Build();
            await repo.AddAsync(pagamento);

            // intencionalmente NÃO chamamos uow.CommitAsync()
        }

        // Em outro escopo (= leitura fora da transação), nada deve aparecer.
        using var verifier = factory.Services.CreateScope();
        var ctx = verifier.ServiceProvider.GetRequiredService<MongoDbContext>();

        var existeAgregado = await ctx.Pagamentos
            .Find(Builders<PagamentoDocument>.Filter.Eq(d => d.Id, pagamentoId))
            .AnyAsync();
        existeAgregado.Should().BeFalse(
            "transação foi abortada no dispose — o documento do agregado NÃO deve estar visível");

        var pendentes = await ctx.Outbox.CountDocumentsAsync(
            Builders<OutboxDocument>.Filter.Eq(d => d.Status, OutboxDocument.StatusPending));
        pendentes.Should().Be(0,
            "outbox docs também participam da transação e devem ter sido descartados");
    }

    [Fact]
    public async Task TransactionalOutbox_DuasEscritasNoMesmoUoW_CompartilhamSessao()
    {
        // Múltiplas escritas dentro do mesmo escopo são comitadas atomicamente — ou tudo ou nada.
        await factory.ResetDatabaseAsync();

        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPagamentoRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await repo.AddAsync(new PagamentoBuilder().WithId(ids[0]).Build());
            await repo.AddAsync(new PagamentoBuilder().WithId(ids[1]).Build());
            await uow.CommitAsync(CancellationToken.None);
        }

        using var verifier = factory.Services.CreateScope();
        var ctx = verifier.ServiceProvider.GetRequiredService<MongoDbContext>();

        var count = await ctx.Pagamentos.CountDocumentsAsync(
            Builders<PagamentoDocument>.Filter.In(d => d.Id, ids));
        count.Should().Be(2, "ambos os Adds participaram da mesma transação e foram comitados juntos");
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
