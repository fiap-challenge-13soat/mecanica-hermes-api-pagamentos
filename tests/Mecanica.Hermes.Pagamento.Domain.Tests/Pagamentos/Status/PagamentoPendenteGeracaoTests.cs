using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Status;
using Mecanica.Hermes.Shared.Core.Domain;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Pagamentos.Status;

public class PagamentoPendenteGeracaoTests
{
    private static readonly Action<IDomainEvent> NoopEvents = _ => { };

    [Fact]
    public void StatusAtual_DeveSerPendenteGeracao()
    {
        var status = new PagamentoPendenteGeracao();

        status.StatusAtual.Should().Be(PagamentoStatus.PendenteGeracao);
    }

    [Fact]
    public void IsTerminal_DeveSerFalse()
    {
        var status = new PagamentoPendenteGeracao();

        status.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void GerarLink_RetornaSucessoComLinkGerado()
    {
        var status = new PagamentoPendenteGeracao();

        var result = status.GerarLink(NoopEvents);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeOfType<PagamentoLinkGerado>();
        status.StatusDestino.Should().Be(PagamentoStatus.LinkGerado);
        status.DataFinalizacao.Should().NotBeNull();
    }

    [Fact]
    public void Expirar_RetornaSucessoComExpirado()
    {
        var status = new PagamentoPendenteGeracao();

        var result = status.Expirar(NoopEvents);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeOfType<PagamentoExpirado>();
        status.StatusDestino.Should().Be(PagamentoStatus.Expirado);
    }

    [Fact]
    public void MarcarEmailEnviado_RetornaFalha()
    {
        var status = new PagamentoPendenteGeracao();

        var result = status.MarcarEmailEnviado(NoopEvents);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Confirmar_RetornaFalha()
    {
        var status = new PagamentoPendenteGeracao();

        var result = status.Confirmar(PagamentoConfirmadoVia.Webhook, NoopEvents);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Recusar_RetornaFalha()
    {
        var status = new PagamentoPendenteGeracao();

        var result = status.Recusar("motivo", NoopEvents);

        result.IsFailure.Should().BeTrue();
    }
}
