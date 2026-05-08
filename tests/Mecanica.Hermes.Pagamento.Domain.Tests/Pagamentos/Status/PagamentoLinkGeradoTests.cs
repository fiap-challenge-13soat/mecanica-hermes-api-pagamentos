using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Status;
using Mecanica.Hermes.Shared.Core.Domain;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Pagamentos.Status;

public class PagamentoLinkGeradoTests
{
    private static readonly Action<IDomainEvent> NoopEvents = _ => { };

    [Fact]
    public void StatusAtual_DeveSerLinkGerado()
    {
        var status = new PagamentoLinkGerado();

        status.StatusAtual.Should().Be(PagamentoStatus.LinkGerado);
    }

    [Fact]
    public void IsTerminal_DeveSerFalse()
    {
        var status = new PagamentoLinkGerado();

        status.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void MarcarEmailEnviado_RetornaSucessoComAguardandoConfirmacao()
    {
        var status = new PagamentoLinkGerado();

        var result = status.MarcarEmailEnviado(NoopEvents);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeOfType<PagamentoAguardandoConfirmacao>();
        status.StatusDestino.Should().Be(PagamentoStatus.AguardandoConfirmacao);
    }

    [Fact]
    public void Recusar_RetornaSucessoComRecusado()
    {
        var status = new PagamentoLinkGerado();

        var result = status.Recusar("motivo", NoopEvents);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeOfType<PagamentoRecusado>();
        status.StatusDestino.Should().Be(PagamentoStatus.Recusado);
    }

    [Fact]
    public void Expirar_RetornaSucessoComExpirado()
    {
        var status = new PagamentoLinkGerado();

        var result = status.Expirar(NoopEvents);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeOfType<PagamentoExpirado>();
        status.StatusDestino.Should().Be(PagamentoStatus.Expirado);
    }

    [Fact]
    public void GerarLink_RetornaFalha()
    {
        var status = new PagamentoLinkGerado();

        var result = status.GerarLink(NoopEvents);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Confirmar_RetornaFalha()
    {
        var status = new PagamentoLinkGerado();

        var result = status.Confirmar(PagamentoConfirmadoVia.Webhook, NoopEvents);

        result.IsFailure.Should().BeTrue();
    }
}
