using FluentAssertions;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Queries.GetPagamentoById;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Moq;
using Xunit;
using PagamentoAggregate = Mecanica.Hermes.Pagamento.Domain.Pagamentos.Pagamento;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Pagamentos.Queries;

public class GetPagamentoByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_PagamentoEncontrado_RetornaDtoOk()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        var reader = new Mock<IPagamentoReader>();
        reader.Setup(r => r.GetByIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        var handler = new GetPagamentoByIdQueryHandler(reader.Object);

        var result = await handler.Handle(new GetPagamentoByIdQuery(pagamento.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Id.Should().Be(pagamento.Id);
        result.Data.OrdemDeServicoId.Should().Be(pagamento.OrdemDeServicoId);
        result.Data.EmailCliente.Should().Be(pagamento.EmailCliente.Valor);
        result.Data.LinkPagamento.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_PagamentoNaoEncontrado_RetornaNotFound()
    {
        var id = Guid.NewGuid();
        var reader = new Mock<IPagamentoReader>();
        reader.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((PagamentoAggregate?)null);

        var handler = new GetPagamentoByIdQueryHandler(reader.Object);

        var result = await handler.Handle(new GetPagamentoByIdQuery(id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}
