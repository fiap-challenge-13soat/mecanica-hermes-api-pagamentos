using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Mecanica.Hermes.Contracts.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using PagamentoAggregate = Mecanica.Hermes.Pagamento.Domain.Pagamentos.Pagamento;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Pagamentos.Consumers.Integration;

public class OrdemDeServicoAguardandoPagamentoConsumerTests
{
    private static (Mock<IPagamentoRepository> repo, Mock<IClienteApiClient> clienteApi) NewMocks()
        => (new Mock<IPagamentoRepository>(), new Mock<IClienteApiClient>());

    private static ServiceProvider BuildProvider(Mock<IPagamentoRepository> repo, Mock<IClienteApiClient> clienteApi)
        => new ServiceCollection()
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<OrdemDeServicoAguardandoPagamentoConsumer>())
            .AddSingleton(repo.Object)
            .AddSingleton(clienteApi.Object)
            .BuildServiceProvider(true);

    [Fact]
    public async Task Consume_HappyPath_PublicaGerarPagamentoRequest()
    {
        var (repo, clienteApi) = NewMocks();
        var ordemId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var cliente = new ClienteDto(clienteId, "cli@x.com", "João Silva", null);

        repo.Setup(r => r.GetByOrdemDeServicoIdAsync(ordemId)).ReturnsAsync((PagamentoAggregate?)null);
        clienteApi.Setup(c => c.GetByIdAsync(clienteId, It.IsAny<CancellationToken>())).ReturnsAsync(cliente);

        await using var provider = BuildProvider(repo, clienteApi);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new OrdemDeServicoAguardandoPagamentoEvent(ordemId, clienteId, 200m));

            (await harness.Consumed.Any<OrdemDeServicoAguardandoPagamentoEvent>()).Should().BeTrue();
            (await harness.Published.Any<GerarPagamentoRequest>(
                ctx => ctx.Context.Message.OrdemDeServicoId == ordemId &&
                       ctx.Context.Message.ClienteId == clienteId &&
                       ctx.Context.Message.EmailCliente == "cli@x.com" &&
                       ctx.Context.Message.Valor == 200m))
                .Should().BeTrue();
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_PagamentoJaExiste_NaoPublicaGerarPagamento()
    {
        var (repo, clienteApi) = NewMocks();
        var ordemId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var existente = PagamentoAggregate
            .Criar(Guid.NewGuid(), ordemId, clienteId, "e@x.com", "Cli", 100m, null)
            .Data!;

        repo.Setup(r => r.GetByOrdemDeServicoIdAsync(ordemId)).ReturnsAsync(existente);

        await using var provider = BuildProvider(repo, clienteApi);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new OrdemDeServicoAguardandoPagamentoEvent(ordemId, clienteId, 200m));

            (await harness.Consumed.Any<OrdemDeServicoAguardandoPagamentoEvent>()).Should().BeTrue();
            (await harness.Published.Any<GerarPagamentoRequest>()).Should().BeFalse();

            clienteApi.Verify(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_ClienteNaoEncontrado_NaoPublicaGerarPagamento()
    {
        var (repo, clienteApi) = NewMocks();
        var ordemId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();

        repo.Setup(r => r.GetByOrdemDeServicoIdAsync(ordemId)).ReturnsAsync((PagamentoAggregate?)null);
        clienteApi.Setup(c => c.GetByIdAsync(clienteId, It.IsAny<CancellationToken>())).ReturnsAsync((ClienteDto?)null);

        await using var provider = BuildProvider(repo, clienteApi);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new OrdemDeServicoAguardandoPagamentoEvent(ordemId, clienteId, 200m));

            (await harness.Consumed.Any<OrdemDeServicoAguardandoPagamentoEvent>()).Should().BeTrue();
            (await harness.Published.Any<GerarPagamentoRequest>()).Should().BeFalse();
        }
        finally
        {
            await harness.Stop();
        }
    }
}
