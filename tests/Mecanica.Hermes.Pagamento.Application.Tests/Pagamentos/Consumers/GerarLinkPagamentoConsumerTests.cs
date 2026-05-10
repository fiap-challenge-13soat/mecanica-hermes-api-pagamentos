using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Mecanica.Hermes.Contracts.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Events;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Shared.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using PagamentoAggregate = Mecanica.Hermes.Pagamento.Domain.Pagamentos.Pagamento;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Pagamentos.Consumers;

public class GerarLinkPagamentoConsumerTests
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
                cfg.AddConsumer<GerarLinkPagamentoConsumer>();
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

    private static ExecuteGerarLinkPagamento NewCommand()
    {
        return new ExecuteGerarLinkPagamento(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "cliente@example.com",
            "Cliente Teste",
            123.45m,
            DateTime.UtcNow.AddDays(7));
    }

    [Fact]
    public async Task Consume_HappyPath_PublicaEnviarEmailEOperacaoCompleted()
    {
        var (repo, uow, mp, metrics, scheduler) = NewMocks();
        var cmd = NewCommand();

        repo.Setup(r => r.GetByIdAsync(cmd.PagamentoId)).ReturnsAsync((PagamentoAggregate?)null);
        mp.Setup(m => m.CreatePreferenceAsync(
                cmd.PagamentoId, cmd.EmailCliente, cmd.NomeCliente,
                cmd.Valor, "BRL", cmd.Vencimento, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MercadoPagoCreatePreferenceResult("PREF-123", "https://mp/pay/123"));

        await using var provider = BuildProvider(repo, uow, mp, metrics, scheduler);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(cmd);

            (await harness.Consumed.Any<ExecuteGerarLinkPagamento>()).Should().BeTrue();
            (await harness.Published.Any<LinkPagamentoGeradoEvent>(
                ctx => ctx.Context.Message.PagamentoId == cmd.PagamentoId))
                .Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();

            repo.Verify(r => r.AddAsync(It.IsAny<PagamentoAggregate>()), Times.Once);
            uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
            metrics.Verify(m => m.PagamentoCriado(), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_PagamentoJaExiste_PublicaApenasOperacaoCompleted()
    {
        var (repo, uow, mp, metrics, scheduler) = NewMocks();
        var cmd = NewCommand();

        var existente = PagamentoBuilderHelperStatic.BuildAguardando(cmd.PagamentoId);
        repo.Setup(r => r.GetByIdAsync(cmd.PagamentoId)).ReturnsAsync(existente);

        await using var provider = BuildProvider(repo, uow, mp, metrics, scheduler);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(cmd);

            (await harness.Consumed.Any<ExecuteGerarLinkPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeTrue();
            (await harness.Published.Any<LinkPagamentoGeradoEvent>()).Should().BeFalse();

            repo.Verify(r => r.AddAsync(It.IsAny<PagamentoAggregate>()), Times.Never);
            mp.Verify(m => m.CreatePreferenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()), Times.Never);
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
        var cmd = NewCommand();

        repo.Setup(r => r.GetByIdAsync(cmd.PagamentoId)).ReturnsAsync((PagamentoAggregate?)null);
        mp.Setup(m => m.CreatePreferenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("MP indisponivel"));

        await using var provider = BuildProvider(repo, uow, mp, metrics, scheduler);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(cmd);

            (await harness.Consumed.Any<ExecuteGerarLinkPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationFailed>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>()).Should().BeFalse();
            (await harness.Published.Any<LinkPagamentoGeradoEvent>()).Should().BeFalse();

            metrics.Verify(m => m.ErroRegistrado(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            repo.Verify(r => r.AddAsync(It.IsAny<PagamentoAggregate>()), Times.Never);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consume_DadosInvalidos_PublicaOperacaoFailedComValidacao()
    {
        var (repo, uow, mp, metrics, scheduler) = NewMocks();

        // ClienteId vazio força creation.IsFailure
        var cmd = new ExecuteGerarLinkPagamento(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            "cliente@example.com",
            "Cliente Teste",
            10m,
            null);

        repo.Setup(r => r.GetByIdAsync(cmd.PagamentoId)).ReturnsAsync((PagamentoAggregate?)null);

        await using var provider = BuildProvider(repo, uow, mp, metrics, scheduler);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(cmd);

            (await harness.Consumed.Any<ExecuteGerarLinkPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationFailed>()).Should().BeTrue();

            mp.Verify(m => m.CreatePreferenceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()), Times.Never);
            metrics.Verify(m => m.ErroRegistrado(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
        finally
        {
            await harness.Stop();
        }
    }

    private static class PagamentoBuilderHelperStatic
    {
        public static PagamentoAggregate BuildAguardando(Guid id)
        {
            var result = PagamentoAggregate.Criar(
                id,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "cli@example.com",
                "Cliente",
                100m,
                DateTime.UtcNow.AddDays(7));
            var p = result.Data!;
            p.GerarLink("PREF", "https://x");
            p.MarcarEmailEnviado();
            return p;
        }
    }
}
