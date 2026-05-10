using FluentAssertions;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Commands.GerarPagamento;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Commands.VerificarStatus;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Pagamentos.Commands;

public class GerarPagamentoCommandTests
{
    [Fact]
    public void ToMessage_ConverteCommandEmGerarPagamentoRequest()
    {
        var pagamentoId = Guid.NewGuid();
        var ordemId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        var vencimento = DateTime.UtcNow.AddDays(7);

        var command = new GerarPagamentoCommand(
            pagamentoId,
            ordemId,
            clienteId,
            "cliente@example.com",
            "Cliente Teste",
            250.55m,
            vencimento);

        var message = command.ToMessage();

        message.Should().BeOfType<GerarPagamentoRequest>();
        var request = (GerarPagamentoRequest)message;
        request.PagamentoId.Should().Be(pagamentoId);
        request.OrdemDeServicoId.Should().Be(ordemId);
        request.ClienteId.Should().Be(clienteId);
        request.EmailCliente.Should().Be("cliente@example.com");
        request.NomeCliente.Should().Be("Cliente Teste");
        request.Valor.Should().Be(250.55m);
        request.Vencimento.Should().Be(vencimento);
    }

    [Fact]
    public void PagamentoId_MarkerInterface_ExpoeId()
    {
        var pagamentoId = Guid.NewGuid();
        var command = new GerarPagamentoCommand(
            pagamentoId, Guid.NewGuid(), Guid.NewGuid(),
            "x@x.com", "X", 1m, null);

        command.PagamentoId.Should().Be(pagamentoId);
    }
}

public class VerificarStatusPagamentoCommandTests
{
    [Fact]
    public void ToMessage_ConverteCommandEmVerificarStatusPagamentoRequest()
    {
        var pagamentoId = Guid.NewGuid();
        var command = new VerificarStatusPagamentoCommand(pagamentoId);

        var message = command.ToMessage();

        message.Should().BeOfType<VerificarStatusPagamentoRequest>();
        ((VerificarStatusPagamentoRequest)message).PagamentoId.Should().Be(pagamentoId);
    }

    [Fact]
    public void PagamentoId_MarkerInterface_ExpoeId()
    {
        var pagamentoId = Guid.NewGuid();
        var command = new VerificarStatusPagamentoCommand(pagamentoId);

        command.PagamentoId.Should().Be(pagamentoId);
    }
}
