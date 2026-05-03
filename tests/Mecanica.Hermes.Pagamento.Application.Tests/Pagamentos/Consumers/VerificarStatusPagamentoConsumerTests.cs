using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Mecanica.Hermes.Application.OrdensDeServico.Messages.Requests;
using Mecanica.Hermes.Pagamento.Application.Common.Persistence;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Events;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using PagamentoAggregate = Mecanica.Hermes.Pagamento.Domain.Pagamentos.Pagamento;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Pagamentos.Consumers;

public class VerificarStatusPagamentoConsumerTests
{
    private static (Mock<IPagamentoRepository> repo,
        Mock<IUnitOfWork> uow,
        Mock<IMercadoPagoClient> mp,
        Mock<IPagamentoMetrics> metrics,
        Mock<IPagamentoSchedulerOptions> scheduler) NewMocks()
    {
        var repo = new Mock<IPagamentoRepository>();
        var uow = new Mock<IUnitOfWork>();
        var mp = new Mock<IMercadoPagoClient>();
        var metrics = new Mock<IPagamentoMetrics>();
        var scheduler = new Mock<IPagamentoSchedulerOptions>();
        scheduler.SetupGet(s => s.PollingInterval).Returns(TimeSpan.FromMinutes(2));
        return (repo, uow, mp, metrics, scheduler);
    }

    private static ServiceProvider BuildProvider(
        Mock<IPagamentoRepository> repo,
        Mock<IUnitOfWork> uow,
        Mock<IMercadoPagoClient> mp,
        Mock<IPagamentoMetrics> metrics,
        Mock<IPagamentoSchedulerOptions> scheduler)
    {
        return new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddDelayedMessageScheduler();
                cfg.AddConsumer<VerificarStatusPagamentoConsumer>();
                cfg.UsingInMemory((context, busCfg) =>
                {
                    busCfg.UseDelayedMessageScheduler();
                    busCfg.ConfigureEndpoints(context);
                });
            })
            .AddSingleton(repo.Object)
            .AddSingleton(uow.Object)
            .AddSingleton(mp.Object)
            .AddSingleton(metrics.Object)
            .AddSingleton(scheduler.Object)
            .BuildServiceProvider(true);
    }

    [Fact]
    public async Task Consume_StatusApproved_ConfirmaEPublicaPagamentoAprovado()
    {
        var (repo, uow, mp, metrics, scheduler) = NewMocks();
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);
        mp.Setup(m => m.GetPaymentStatusAsync(pagamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPaymentStatus(MercadoPagoPaymentOutcome.Approved, "approved"));

        await using var provider = BuildProvider(repo, uow, mp, metrics, scheduler);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteVerificarStatusPagamento(pagamento.Id));

            (await harness.Consumed.Any<ExecuteVerificarStatusPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoAprovadoRequest>(
                ctx => ctx.Context.Message.OrdemDeServicoId == pagamento.OrdemDeServicoId))
                .Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();

            repo.Verify(r => r.UpdateAsync(pagamento), Times.Once);
            metrics.Verify(m => m.PagamentoConfirmado(It.IsAny<string>()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_StatusRejected_RecusaEPublicaIntegrationEvent()
    {
        var (repo, uow, mp, metrics, scheduler) = NewMocks();
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);
        mp.Setup(m => m.GetPaymentStatusAsync(pagamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPaymentStatus(MercadoPagoPaymentOutcome.Rejected, "cc_rejected"));

        await using var provider = BuildProvider(repo, uow, mp, metrics, scheduler);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteVerificarStatusPagamento(pagamento.Id));

            (await harness.Consumed.Any<ExecuteVerificarStatusPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoRecusadoIntegrationEvent>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoAprovadoRequest>()).Should().BeFalse();

            metrics.Verify(m => m.PagamentoRecusado(), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_StatusPendingComTentativasRestantes_Reagenda()
    {
        var (repo, uow, mp, metrics, scheduler) = NewMocks();
        // Limite alto, tentativas zero → ainda pode reverificar.
        var pagamento = new PagamentoBuilder().WithLimiteVerificacoes(15).BuildAguardandoConfirmacao();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);
        mp.Setup(m => m.GetPaymentStatusAsync(pagamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPaymentStatus(MercadoPagoPaymentOutcome.Pending, "pending"));

        await using var provider = BuildProvider(repo, uow, mp, metrics, scheduler);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteVerificarStatusPagamento(pagamento.Id));

            (await harness.Consumed.Any<ExecuteVerificarStatusPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoAprovadoRequest>()).Should().BeFalse();
            (await harness.Published.Any<PagamentoRecusadoIntegrationEvent>()).Should().BeFalse();

            // Tentativas incrementadas mas estado não terminal.
            pagamento.TentativasVerificacao.Should().Be(1);
            pagamento.PodeReverificar.Should().BeTrue();
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_StatusPendingNoLimite_ExpiraEPublicaIntegrationEvent()
    {
        var (repo, uow, mp, metrics, scheduler) = NewMocks();
        // Limite 1 e a chamada incrementa tentativa para 1 → não pode mais reverificar.
        var pagamento = new PagamentoBuilder().WithLimiteVerificacoes(1).BuildAguardandoConfirmacao();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);
        mp.Setup(m => m.GetPaymentStatusAsync(pagamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPaymentStatus(MercadoPagoPaymentOutcome.Pending, "pending"));

        await using var provider = BuildProvider(repo, uow, mp, metrics, scheduler);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteVerificarStatusPagamento(pagamento.Id));

            (await harness.Consumed.Any<ExecuteVerificarStatusPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoRecusadoIntegrationEvent>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();

            metrics.Verify(m => m.PagamentoExpirado(), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_StatusExpired_ExpiraEPublicaIntegrationEvent()
    {
        var (repo, uow, mp, metrics, scheduler) = NewMocks();
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);
        mp.Setup(m => m.GetPaymentStatusAsync(pagamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoPaymentStatus(MercadoPagoPaymentOutcome.Expired, "expired"));

        await using var provider = BuildProvider(repo, uow, mp, metrics, scheduler);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteVerificarStatusPagamento(pagamento.Id));

            (await harness.Consumed.Any<ExecuteVerificarStatusPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoRecusadoIntegrationEvent>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();

            metrics.Verify(m => m.PagamentoExpirado(), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_PagamentoNaoEncontrado_PublicaOperacaoFailed()
    {
        var (repo, uow, mp, metrics, scheduler) = NewMocks();
        var id = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((PagamentoAggregate?)null);

        await using var provider = BuildProvider(repo, uow, mp, metrics, scheduler);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteVerificarStatusPagamento(id));

            (await harness.Consumed.Any<ExecuteVerificarStatusPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationFailed>()).Should().BeTrue();

            mp.Verify(m => m.GetPaymentStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_MercadoPagoLancaExcecao_PublicaOperacaoFailed()
    {
        var (repo, uow, mp, metrics, scheduler) = NewMocks();
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);
        mp.Setup(m => m.GetPaymentStatusAsync(pagamento.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("MP timeout"));

        await using var provider = BuildProvider(repo, uow, mp, metrics, scheduler);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteVerificarStatusPagamento(pagamento.Id));

            (await harness.Consumed.Any<ExecuteVerificarStatusPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationFailed>()).Should().BeTrue();

            metrics.Verify(m => m.MercadoPagoConsultaDuracao(It.IsAny<double>()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
