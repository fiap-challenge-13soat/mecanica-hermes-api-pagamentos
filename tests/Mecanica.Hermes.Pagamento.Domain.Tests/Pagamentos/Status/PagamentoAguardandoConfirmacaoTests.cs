using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Status;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Pagamentos.Status;

public class PagamentoAguardandoConfirmacaoTests
{
    private static readonly Action<IDomainEvent> NoopEvents = _ => { };

    [Fact]
    public void StatusAtual_DeveSerAguardandoConfirmacao()
    {
        var status = new PagamentoAguardandoConfirmacao();

        status.StatusAtual.Should().Be(PagamentoStatus.AguardandoConfirmacao);
    }

    [Fact]
    public void IsTerminal_DeveSerFalse()
    {
        var status = new PagamentoAguardandoConfirmacao();

        status.IsTerminal.Should().BeFalse();
    }

    [Theory]
    [InlineData(PagamentoConfirmadoVia.Webhook)]
    [InlineData(PagamentoConfirmadoVia.Polling)]
    public void Confirmar_RetornaSucessoComPago(PagamentoConfirmadoVia via)
    {
        var status = new PagamentoAguardandoConfirmacao();

        var result = status.Confirmar(via, NoopEvents);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeOfType<PagamentoPago>();
        status.StatusDestino.Should().Be(PagamentoStatus.Pago);
    }

    [Fact]
    public void Recusar_RetornaSucessoComRecusado()
    {
        var status = new PagamentoAguardandoConfirmacao();

        var result = status.Recusar("motivo", NoopEvents);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeOfType<PagamentoRecusado>();
        status.StatusDestino.Should().Be(PagamentoStatus.Recusado);
    }

    [Fact]
    public void Expirar_RetornaSucessoComExpirado()
    {
        var status = new PagamentoAguardandoConfirmacao();

        var result = status.Expirar(NoopEvents);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeOfType<PagamentoExpirado>();
        status.StatusDestino.Should().Be(PagamentoStatus.Expirado);
    }

    [Fact]
    public void GerarLink_RetornaFalha()
    {
        var status = new PagamentoAguardandoConfirmacao();

        var result = status.GerarLink(NoopEvents);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarEmailEnviado_RetornaFalha()
    {
        var status = new PagamentoAguardandoConfirmacao();

        var result = status.MarcarEmailEnviado(NoopEvents);

        result.IsFailure.Should().BeTrue();
    }
}
