using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Mecanica.Hermes.Contracts.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Mecanica.Hermes.Shared.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using PagamentoAggregate = Mecanica.Hermes.Pagamento.Domain.Pagamentos.Pagamento;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Pagamentos.Consumers.Integration;

public class OrdemDeServicoCanceladaConsumerTests
{
    private static OrdemDeServicoCanceladaEvent NewEvent(Guid? ordemId = null) =>
        new(ordemId ?? Guid.NewGuid(), "AguardandoPagamento", DateTime.UtcNow);

    private static ServiceProvider BuildProvider(
        Mock<IPagamentoRepository> repo,
        Mock<IUnitOfWork> uow) =>
        new ServiceCollection()
            .AddMassTransitTestHarness(cfg => cfg.AddConsumer<OrdemDeServicoCanceladaConsumer>())
            .AddSingleton(repo.Object)
            .AddSingleton(uow.Object)
            .AddSingleton<ILogger<OrdemDeServicoCanceladaConsumer>>(
                NullLogger<OrdemDeServicoCanceladaConsumer>.Instance)
            .BuildServiceProvider(true);

    [Fact]
    public async Task Consume_PagamentoAtivo_RecusaEPersiste()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        var evento = NewEvent(pagamento.OrdemDeServicoId);

        var repo = new Mock<IPagamentoRepository>();
        repo.Setup(r => r.GetByOrdemDeServicoIdAsync(pagamento.OrdemDeServicoId)).ReturnsAsync(pagamento);
        var uow = new Mock<IUnitOfWork>();

        await using var provider = BuildProvider(repo, uow);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(evento);

            (await harness.Consumed.Any<OrdemDeServicoCanceladaEvent>()).Should().BeTrue();

            repo.Verify(r => r.UpdateAsync(pagamento), Times.Once);
            uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_SemPagamentoAssociado_NaoFazNada()
    {
        var evento = NewEvent();

        var repo = new Mock<IPagamentoRepository>();
        repo.Setup(r => r.GetByOrdemDeServicoIdAsync(evento.OrdemDeServicoId))
            .ReturnsAsync((PagamentoAggregate?)null);
        var uow = new Mock<IUnitOfWork>();

        await using var provider = BuildProvider(repo, uow);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(evento);

            (await harness.Consumed.Any<OrdemDeServicoCanceladaEvent>()).Should().BeTrue();

            repo.Verify(r => r.UpdateAsync(It.IsAny<PagamentoAggregate>()), Times.Never);
            uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_PagamentoJaTerminal_NaoFazNada()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        var recusarPrevio = pagamento.Recusar("Recusa anterior");
        recusarPrevio.IsSuccess.Should().BeTrue();
        pagamento.StatusAtual.IsTerminal.Should().BeTrue();

        var evento = NewEvent(pagamento.OrdemDeServicoId);

        var repo = new Mock<IPagamentoRepository>();
        repo.Setup(r => r.GetByOrdemDeServicoIdAsync(pagamento.OrdemDeServicoId)).ReturnsAsync(pagamento);
        var uow = new Mock<IUnitOfWork>();

        await using var provider = BuildProvider(repo, uow);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(evento);

            (await harness.Consumed.Any<OrdemDeServicoCanceladaEvent>()).Should().BeTrue();

            repo.Verify(r => r.UpdateAsync(It.IsAny<PagamentoAggregate>()), Times.Never);
            uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_PagamentoEmEstadoNaoRecusavel_NaoPersiste()
    {
        // Pagamento em PendenteGeracao não é terminal mas tampouco aceita Recusar.
        var pagamento = new PagamentoBuilder().Build();
        pagamento.StatusAtual.IsTerminal.Should().BeFalse();

        var evento = NewEvent(pagamento.OrdemDeServicoId);

        var repo = new Mock<IPagamentoRepository>();
        repo.Setup(r => r.GetByOrdemDeServicoIdAsync(pagamento.OrdemDeServicoId)).ReturnsAsync(pagamento);
        var uow = new Mock<IUnitOfWork>();

        await using var provider = BuildProvider(repo, uow);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(evento);

            (await harness.Consumed.Any<OrdemDeServicoCanceladaEvent>()).Should().BeTrue();

            repo.Verify(r => r.UpdateAsync(It.IsAny<PagamentoAggregate>()), Times.Never);
            uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
