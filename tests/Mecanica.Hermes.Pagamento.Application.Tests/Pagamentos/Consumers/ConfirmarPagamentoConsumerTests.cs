using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Mecanica.Hermes.Application.OrdensDeServico.Messages.Requests;
using Mecanica.Hermes.Pagamento.Application.Common.Persistence;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Events;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Pagamentos.Consumers;

public class ConfirmarPagamentoConsumerTests
{
    [Fact]
    public async Task Confirmar_PublicaCallbackParaOSeOperationCompleted()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();

        var repo = new Mock<IPagamentoRepository>();
        repo.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        var uow = new Mock<IUnitOfWork>();
        var metrics = new Mock<IPagamentoMetrics>();

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg => { cfg.AddConsumer<ConfirmarPagamentoConsumer>(); })
            .AddSingleton(repo.Object)
            .AddSingleton(uow.Object)
            .AddSingleton(metrics.Object)
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new ExecuteConfirmarPagamento(pagamento.Id, PagamentoConfirmadoVia.Webhook));

            (await harness.Consumed.Any<ExecuteConfirmarPagamento>()).Should().BeTrue();
            (await harness.Published.Any<PagamentoAprovadoRequest>(
                ctx => ctx.Context.Message.OrdemDeServicoId == pagamento.OrdemDeServicoId))
                .Should().BeTrue();
            (await harness.Published.Any<PagamentoOperationCompleted>())
                .Should().BeTrue();
        }
        finally
        {
            await harness.Stop();
        }
    }
}
