using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Mecanica.Hermes.Pagamento.Application.Common.Persistence;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Events;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using PagamentoAggregate = Mecanica.Hermes.Pagamento.Domain.Pagamentos.Pagamento;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Pagamentos.Consumers;

public class ExpirarPagamentoConsumerTests
{
    [Fact]
    public async Task Expirar_PublicaIntegrationEventEOperacaoCompleted()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();

        var repo = new Mock<IPagamentoRepository>();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        var uow = new Mock<IUnitOfWork>();
        var metrics = new Mock<IPagamentoMetrics>();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg => { cfg.AddConsumer<ExpirarPagamentoConsumer>(); })
            .AddSingleton(repo.Object)
            .AddSingleton(uow.Object)
            .AddSingleton(metrics.Object)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteExpirarPagamento(pagamento.Id));

            (await harness.Consumed.Any<ExecuteExpirarPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoRecusadoIntegrationEvent>(
                ctx => ctx.Context.Message.PagamentoId == pagamento.Id))
                .Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();

            repo.Verify(r => r.UpdateAsync(pagamento), Times.Once);
            uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            metrics.Verify(m => m.PagamentoExpirado(), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Expirar_PagamentoNaoEncontrado_PublicaOperacaoFailed()
    {
        var id = Guid.NewGuid();
        var repo = new Mock<IPagamentoRepository>();
        repo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((PagamentoAggregate?)null);

        var uow = new Mock<IUnitOfWork>();
        var metrics = new Mock<IPagamentoMetrics>();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg => { cfg.AddConsumer<ExpirarPagamentoConsumer>(); })
            .AddSingleton(repo.Object)
            .AddSingleton(uow.Object)
            .AddSingleton(metrics.Object)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteExpirarPagamento(id));

            (await harness.Consumed.Any<ExecuteExpirarPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationFailed>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoRecusadoIntegrationEvent>()).Should().BeFalse();

            metrics.Verify(m => m.ErroRegistrado(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Expirar_StatusInvalido_PublicaOperacaoFailed()
    {
        // Pagamento em estado terminal (Pago) não pode ser expirado.
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.Confirmar(PagamentoConfirmadoVia.Webhook);

        var repo = new Mock<IPagamentoRepository>();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        var uow = new Mock<IUnitOfWork>();
        var metrics = new Mock<IPagamentoMetrics>();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg => { cfg.AddConsumer<ExpirarPagamentoConsumer>(); })
            .AddSingleton(repo.Object)
            .AddSingleton(uow.Object)
            .AddSingleton(metrics.Object)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteExpirarPagamento(pagamento.Id));

            (await harness.Consumed.Any<ExecuteExpirarPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationFailed>()).Should().BeTrue();

            repo.Verify(r => r.UpdateAsync(It.IsAny<PagamentoAggregate>()), Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
