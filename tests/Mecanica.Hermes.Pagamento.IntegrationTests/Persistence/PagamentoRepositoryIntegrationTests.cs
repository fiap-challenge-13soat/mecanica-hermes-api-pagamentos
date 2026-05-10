using FluentAssertions;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using Mecanica.Hermes.Pagamento.IntegrationTests.Infrastructure;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Mecanica.Hermes.Shared.Core.Pagination;
using Mecanica.Hermes.Shared.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;
using PagamentoAggregate = Mecanica.Hermes.Pagamento.Domain.Pagamentos.Pagamento;

namespace Mecanica.Hermes.Pagamento.IntegrationTests.Persistence;

[Collection(IntegrationTestCollection.Name)]
public sealed class PagamentoRepositoryIntegrationTests(IntegrationTestWebApplicationFactory factory)
{
    [Fact]
    public async Task AddAsync_GetByIdAsync_PreservaAgregadoCompleto()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPagamentoRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var pagamento = new PagamentoBuilder().Build();

        await repo.AddAsync(pagamento);
        await uow.CommitAsync(CancellationToken.None);

        var recuperado = await repo.GetByIdAsync(pagamento.Id);
        recuperado.Should().NotBeNull();
        recuperado!.Id.Should().Be(pagamento.Id);
        recuperado.OrdemDeServicoId.Should().Be(pagamento.OrdemDeServicoId);
        recuperado.ClienteId.Should().Be(pagamento.ClienteId);
        recuperado.EmailCliente.Valor.Should().Be(pagamento.EmailCliente.Valor);
        recuperado.NomeCliente.Should().Be(pagamento.NomeCliente);
        recuperado.Valor.Valor.Should().Be(pagamento.Valor.Valor);
        recuperado.Valor.Moeda.Should().Be(pagamento.Valor.Moeda);
        recuperado.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.PendenteGeracao);
    }

    [Fact]
    public async Task UpdateAsync_AposGerarLink_PersisteNovoStatusEHistorico()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPagamentoRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var pagamento = new PagamentoBuilder().Build();
        await repo.AddAsync(pagamento);
        await uow.CommitAsync(CancellationToken.None);

        var gerarLink = pagamento.GerarLink("PREF-123", "https://mp.example/pay/abc");
        gerarLink.IsSuccess.Should().BeTrue();

        await repo.UpdateAsync(pagamento);
        await uow.CommitAsync(CancellationToken.None);

        var recuperado = await repo.GetByIdAsync(pagamento.Id);
        recuperado!.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.LinkGerado);
        recuperado.LinkPagamento.Should().NotBeNull();
        recuperado.LinkPagamento!.PreferenceId.Should().Be("PREF-123");
        recuperado.LinkPagamento.Url.Should().Be("https://mp.example/pay/abc");
        recuperado.HistoricosStatus.Should().HaveCountGreaterThan(0,
            "a transição PendenteGeracao→LinkGerado deve gerar entrada no histórico");
    }

    [Fact]
    public async Task UpdateAsync_PagamentoInexistente_LancaConcurrencyConflictException()
    {
        // Cenário: alguém apaga o documento entre o Get e o Update (manutenção / bug).
        // O ReplaceOneAsync com IsUpsert=false retorna MatchedCount=0 e o repositório
        // deve traduzir para ConcurrencyConflictException (defesa contra escrita órfã).
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPagamentoRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var ctx = scope.ServiceProvider.GetRequiredService<MongoDbContext>();

        var pagamento = new PagamentoBuilder().Build();
        await repo.AddAsync(pagamento);
        await uow.CommitAsync(CancellationToken.None);

        // Apaga o documento por baixo dos panos para simular drift.
        await ctx.Pagamentos.DeleteOneAsync(
            Builders<PagamentoDocument>.Filter.Eq(d => d.Id, pagamento.Id));

        pagamento.GerarLink("PREF-X", "https://mp.example/x");

        var act = async () =>
        {
            await repo.UpdateAsync(pagamento);
            await uow.CommitAsync(CancellationToken.None);
        };

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    [Fact]
    public async Task GetByOrdemDeServicoIdAsync_FiltraPeloId()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPagamentoRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var ordemAlvo = Guid.NewGuid();
        var alvo = new PagamentoBuilder().WithOrdemDeServicoId(ordemAlvo).Build();
        var ruido1 = new PagamentoBuilder().Build();
        var ruido2 = new PagamentoBuilder().Build();

        foreach (var p in new[] { alvo, ruido1, ruido2 })
            await repo.AddAsync(p);
        await uow.CommitAsync(CancellationToken.None);

        var encontrado = await repo.GetByOrdemDeServicoIdAsync(ordemAlvo);
        encontrado.Should().NotBeNull();
        encontrado!.Id.Should().Be(alvo.Id);
    }

    [Fact]
    public async Task ListByStatusAsync_FiltraEPagina()
    {
        await factory.ResetDatabaseAsync();
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPagamentoRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // 3 pagamentos AguardandoConfirmacao + 2 PendenteGeracao
        for (var i = 0; i < 3; i++)
            await repo.AddAsync(new PagamentoBuilder().BuildAguardandoConfirmacao());
        for (var i = 0; i < 2; i++)
            await repo.AddAsync(new PagamentoBuilder().Build());
        await uow.CommitAsync(CancellationToken.None);

        var pagina1 = await repo.ListByStatusAsync(
            PagamentoStatus.AguardandoConfirmacao,
            new PaginationParams { Page = 1, PageSize = 2 });

        pagina1.TotalCount.Should().Be(3);
        pagina1.Items.Should().HaveCount(2);
        pagina1.HasNextPage.Should().BeTrue();

        var pagina2 = await repo.ListByStatusAsync(
            PagamentoStatus.AguardandoConfirmacao,
            new PaginationParams { Page = 2, PageSize = 2 });

        pagina2.Items.Should().HaveCount(1);
        pagina2.HasNextPage.Should().BeFalse();
    }
}
