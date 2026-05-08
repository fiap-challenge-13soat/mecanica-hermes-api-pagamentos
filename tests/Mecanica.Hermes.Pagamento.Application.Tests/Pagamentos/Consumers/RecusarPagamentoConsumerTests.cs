using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Mecanica.Hermes.Shared.Core.Persistence;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Events;
using Mecanica.Hermes.Contracts.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using PagamentoAggregate = Mecanica.Hermes.Pagamento.Domain.Pagamentos.Pagamento;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Pagamentos.Consumers;

public class RecusarPagamentoConsumerTests
{
    [Fact]
    public async Task Recusar_PublicaIntegrationEventEOperacaoCompleted()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        var motivo = "Cartão sem limite";

        var repo = new Mock<IPagamentoRepository>();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        var uow = new Mock<IUnitOfWork>();
        var metrics = new Mock<IPagamentoMetrics>();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg => { cfg.AddConsumer<RecusarPagamentoConsumer>(); })
            .AddSingleton(repo.Object)
            .AddSingleton(uow.Object)
            .AddSingleton(metrics.Object)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteRecusarPagamento(pagamento.Id, motivo));

            (await harness.Consumed.Any<ExecuteRecusarPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoRecusadoEvent>(
                ctx => ctx.Context.Message.Motivo == motivo &&
                       ctx.Context.Message.OrdemDeServicoId == pagamento.OrdemDeServicoId))
                .Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();

            repo.Verify(r => r.UpdateAsync(pagamento), Times.Once);
            uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            metrics.Verify(m => m.PagamentoRecusado(), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Recusar_PagamentoNaoEncontrado_PublicaOperacaoFailed()
    {
        var id = Guid.NewGuid();
        var repo = new Mock<IPagamentoRepository>();
        repo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((PagamentoAggregate?)null);

        var uow = new Mock<IUnitOfWork>();
        var metrics = new Mock<IPagamentoMetrics>();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg => { cfg.AddConsumer<RecusarPagamentoConsumer>(); })
            .AddSingleton(repo.Object)
            .AddSingleton(uow.Object)
            .AddSingleton(metrics.Object)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteRecusarPagamento(id, "qualquer"));

            (await harness.Consumed.Any<ExecuteRecusarPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationFailed>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoRecusadoEvent>()).Should().BeFalse();

            metrics.Verify(m => m.ErroRegistrado(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Recusar_StatusInvalido_PublicaOperacaoFailed()
    {
        // Pagamento em estado PendenteGeracao não pode ser recusado.
        var pagamento = new PagamentoBuilder().Build();

        var repo = new Mock<IPagamentoRepository>();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        var uow = new Mock<IUnitOfWork>();
        var metrics = new Mock<IPagamentoMetrics>();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg => { cfg.AddConsumer<RecusarPagamentoConsumer>(); })
            .AddSingleton(repo.Object)
            .AddSingleton(uow.Object)
            .AddSingleton(metrics.Object)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteRecusarPagamento(pagamento.Id, "motivo"));

            (await harness.Consumed.Any<ExecuteRecusarPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationFailed>()).Should().BeTrue();

            repo.Verify(r => r.UpdateAsync(It.IsAny<PagamentoAggregate>()), Times.Never);
            uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
