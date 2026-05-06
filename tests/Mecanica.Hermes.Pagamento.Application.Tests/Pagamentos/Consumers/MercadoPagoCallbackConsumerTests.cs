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

public class MercadoPagoCallbackConsumerTests
{
    private static (Mock<IPagamentoRepository> repo,
        Mock<IUnitOfWork> uow,
        Mock<IPagamentoMetrics> metrics) NewMocks()
    {
        return (new Mock<IPagamentoRepository>(),
            new Mock<IUnitOfWork>(),
            new Mock<IPagamentoMetrics>());
    }

    private static ServiceProvider BuildProvider(
        Mock<IPagamentoRepository> repo,
        Mock<IUnitOfWork> uow,
        Mock<IPagamentoMetrics> metrics)
    {
        return new ServiceCollection()
            .AddMassTransitTestHarness(cfg => { cfg.AddConsumer<MercadoPagoCallbackConsumer>(); })
            .AddSingleton(repo.Object)
            .AddSingleton(uow.Object)
            .AddSingleton(metrics.Object)
            .BuildServiceProvider(true);
    }

    [Fact]
    public async Task Consume_StatusApproved_PublicaPagamentoConfirmado()
    {
        var (repo, uow, metrics) = NewMocks();
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        await using var provider = BuildProvider(repo, uow, metrics);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteMercadoPagoCallback(pagamento.Id, "approved"));

            (await harness.Consumed.Any<ExecuteMercadoPagoCallback>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoConfirmadoEvent>(
                ctx => ctx.Context.Message.OrdemDeServicoId == pagamento.OrdemDeServicoId))
                .Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();

            metrics.Verify(m => m.WebhookRecebido("approved"), Times.Once);
            metrics.Verify(m => m.PagamentoConfirmado(PagamentoConfirmadoVia.Webhook.ToString()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_StatusApprovedJaPago_PublicaApenasCallback()
    {
        var (repo, uow, metrics) = NewMocks();
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.Confirmar(PagamentoConfirmadoVia.Polling);
        pagamento.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.Pago);

        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        await using var provider = BuildProvider(repo, uow, metrics);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteMercadoPagoCallback(pagamento.Id, "approved"));

            (await harness.Consumed.Any<ExecuteMercadoPagoCallback>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoConfirmadoEvent>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();

            repo.Verify(r => r.UpdateAsync(It.IsAny<PagamentoAggregate>()), Times.Never);
            uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_StatusRejected_PublicaRecusadoEvent()
    {
        var (repo, uow, metrics) = NewMocks();
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        await using var provider = BuildProvider(repo, uow, metrics);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteMercadoPagoCallback(pagamento.Id, "rejected"));

            (await harness.Consumed.Any<ExecuteMercadoPagoCallback>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoRecusadoEvent>(
                ctx => ctx.Context.Message.PagamentoId == pagamento.Id))
                .Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();

            metrics.Verify(m => m.PagamentoRecusado(), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_StatusCancelled_PublicaRecusadoEvent()
    {
        var (repo, uow, metrics) = NewMocks();
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        await using var provider = BuildProvider(repo, uow, metrics);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteMercadoPagoCallback(pagamento.Id, "cancelled"));

            (await harness.Consumed.Any<ExecuteMercadoPagoCallback>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoRecusadoEvent>()).Should().BeTrue();
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_StatusPending_NaoMutaEstado()
    {
        var (repo, uow, metrics) = NewMocks();
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        var statusAntes = pagamento.StatusAtual.StatusAtual;
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        await using var provider = BuildProvider(repo, uow, metrics);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteMercadoPagoCallback(pagamento.Id, "pending"));

            (await harness.Consumed.Any<ExecuteMercadoPagoCallback>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoConfirmadoEvent>()).Should().BeFalse();
            (await harness.Published.Any<PagamentoRecusadoEvent>()).Should().BeFalse();

            pagamento.StatusAtual.StatusAtual.Should().Be(statusAntes);
            repo.Verify(r => r.UpdateAsync(It.IsAny<PagamentoAggregate>()), Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_StatusDesconhecido_RegistraOutcomeENaoMutaEstado()
    {
        var (repo, uow, metrics) = NewMocks();
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        await using var provider = BuildProvider(repo, uow, metrics);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteMercadoPagoCallback(pagamento.Id, "qualquer_outro"));

            (await harness.Consumed.Any<ExecuteMercadoPagoCallback>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();

            metrics.Verify(m => m.WebhookRecebido("qualquer_outro"), Times.Once);
            repo.Verify(r => r.UpdateAsync(It.IsAny<PagamentoAggregate>()), Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_PagamentoNaoEncontrado_PublicaOperacaoFailed()
    {
        var (repo, uow, metrics) = NewMocks();
        var id = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((PagamentoAggregate?)null);

        await using var provider = BuildProvider(repo, uow, metrics);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteMercadoPagoCallback(id, "approved"));

            (await harness.Consumed.Any<ExecuteMercadoPagoCallback>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationFailed>()).Should().BeTrue();

            metrics.Verify(m => m.ErroRegistrado(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
