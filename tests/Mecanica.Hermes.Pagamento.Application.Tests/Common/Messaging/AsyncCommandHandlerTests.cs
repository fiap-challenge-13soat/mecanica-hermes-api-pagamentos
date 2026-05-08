using FluentAssertions;
using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Commands.GerarPagamento;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;
using Mecanica.Hermes.Shared.Application.Messaging;
using Moq;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Common.Messaging;

public class AsyncCommandHandlerTests
{
    [Fact]
    public async Task Handle_PublicaMensagemERetornaAccepted()
    {
        var publisher = new Mock<IAsyncMessagePublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AsyncCommandHandler<GerarPagamentoCommand>(publisher.Object);

        var command = new GerarPagamentoCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "cliente@example.com",
            "Cliente Teste",
            100m,
            null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted);

        publisher.Verify(p => p.Publish(
            It.Is<object>(m => m is GerarPagamentoRequest && ((GerarPagamentoRequest)m).PagamentoId == command.PagamentoId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PropagaCancellationToken()
    {
        var publisher = new Mock<IAsyncMessagePublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new AsyncCommandHandler<GerarPagamentoCommand>(publisher.Object);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var command = new GerarPagamentoCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "cliente@example.com", "Cliente", 100m, null);

        await handler.Handle(command, token);

        publisher.Verify(p => p.Publish(It.IsAny<object>(), token), Times.Once);
    }
}
