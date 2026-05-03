using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Status;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Pagamentos.Status;

public class EstadosTerminaisTests
{
    private static readonly Action<IDomainEvent> NoopEvents = _ => { };

    public static IEnumerable<object[]> EstadosTerminais()
    {
        yield return new object[] { new PagamentoPago(), PagamentoStatus.Pago };
        yield return new object[] { new PagamentoRecusado(), PagamentoStatus.Recusado };
        yield return new object[] { new PagamentoExpirado(), PagamentoStatus.Expirado };
    }

    [Theory]
    [MemberData(nameof(EstadosTerminais))]
    public void StatusAtual_RefleteOEstadoTerminal(PagamentoStatusBase status, PagamentoStatus esperado)
    {
        status.StatusAtual.Should().Be(esperado);
    }

    [Theory]
    [MemberData(nameof(EstadosTerminais))]
    public void IsTerminal_DeveSerTrue(PagamentoStatusBase status, PagamentoStatus _)
    {
        status.IsTerminal.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(EstadosTerminais))]
    public void GerarLink_RetornaFalha(PagamentoStatusBase status, PagamentoStatus _)
    {
        status.GerarLink(NoopEvents).IsFailure.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(EstadosTerminais))]
    public void MarcarEmailEnviado_RetornaFalha(PagamentoStatusBase status, PagamentoStatus _)
    {
        status.MarcarEmailEnviado(NoopEvents).IsFailure.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(EstadosTerminais))]
    public void Confirmar_RetornaFalha(PagamentoStatusBase status, PagamentoStatus _)
    {
        status.Confirmar(PagamentoConfirmadoVia.Webhook, NoopEvents).IsFailure.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(EstadosTerminais))]
    public void Recusar_RetornaFalha(PagamentoStatusBase status, PagamentoStatus _)
    {
        status.Recusar("motivo", NoopEvents).IsFailure.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(EstadosTerminais))]
    public void Expirar_RetornaFalha(PagamentoStatusBase status, PagamentoStatus _)
    {
        status.Expirar(NoopEvents).IsFailure.Should().BeTrue();
    }
}
