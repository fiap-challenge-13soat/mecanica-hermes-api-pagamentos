using FluentAssertions;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using Mecanica.Hermes.Pagamento.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Mecanica.Hermes.Pagamento.IntegrationTests.Persistence;

[Collection(IntegrationTestCollection.Name)]
public sealed class WebhookEventRepositoryIntegrationTests(IntegrationTestWebApplicationFactory factory)
{
    [Fact]
    public async Task TryRegisterAsync_PrimeiraOcorrencia_PersisteERetornaTrue()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWebhookEventRepository>();
        var ctx = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

        var eventId = "mp-evt-" + Guid.NewGuid().ToString("N");
        var pagamentoId = Guid.NewGuid();

        var ok = await repo.TryRegisterAsync(eventId, pagamentoId, "approved", CancellationToken.None);

        ok.Should().BeTrue();

        var doc = await ctx.WebhookEvents
            .Find(d => d.MercadoPagoEventId == eventId)
            .FirstOrDefaultAsync();
        doc.Should().NotBeNull();
        doc!.PagamentoId.Should().Be(pagamentoId);
        doc.Status.Should().Be("approved");
    }

    [Fact]
    public async Task TryRegisterAsync_Duplicado_RetornaFalseENaoDuplicaDocumento()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWebhookEventRepository>();
        var ctx = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

        var eventId = "mp-evt-dup-" + Guid.NewGuid().ToString("N");
        var pagamentoId = Guid.NewGuid();

        (await repo.TryRegisterAsync(eventId, pagamentoId, "approved", CancellationToken.None))
            .Should().BeTrue();

        // Segunda chamada com MESMO eventId → unique violation no _id, retorna false sem lançar.
        (await repo.TryRegisterAsync(eventId, pagamentoId, "approved", CancellationToken.None))
            .Should().BeFalse();

        var count = await ctx.WebhookEvents.CountDocumentsAsync(
            FilterDefinition<WebhookEventDocument>.Empty);
        count.Should().Be(1, "a chave duplicada não deve gerar segunda inserção");
    }
}
