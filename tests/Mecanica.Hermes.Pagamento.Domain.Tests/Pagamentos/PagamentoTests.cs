using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Pagamentos;

public class PagamentoTests
{
    [Fact]
    public void Criar_ComDadosValidos_ProduzPagamentoEmPendenteGeracao()
    {
        var pagamento = new PagamentoBuilder().Build();

        pagamento.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.PendenteGeracao);
        pagamento.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void Criar_ComEmailInvalido_RetornaFalha()
    {
        var result = Domain.Pagamentos.Pagamento.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "nao-eh-email",
            "Joao",
            10m);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("-mail", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Criar_ComValorAbaixoDoMinimo_RetornaFalha()
    {
        var result = Domain.Pagamentos.Pagamento.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "joao@example.com",
            "Joao",
            0m);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GerarLink_TransicionaParaLinkGerado()
    {
        var pagamento = new PagamentoBuilder().Build();
        var result = pagamento.GerarLink("PREF-1", "https://mp.example/pay");

        result.IsSuccess.Should().BeTrue();
        pagamento.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.LinkGerado);
        pagamento.LinkPagamento!.PreferenceId.Should().Be("PREF-1");
    }

    [Fact]
    public void Confirmar_DepoisDeAguardandoConfirmacao_TransicionaParaPago()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();

        var result = pagamento.Confirmar(PagamentoConfirmadoVia.Webhook);

        result.IsSuccess.Should().BeTrue();
        pagamento.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.Pago);
        pagamento.ConfirmadoVia.Should().Be(PagamentoConfirmadoVia.Webhook);
    }

    [Fact]
    public void Recusar_DepoisDeAguardandoConfirmacao_TransicionaParaRecusado()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();

        var result = pagamento.Recusar("Cartão recusado pelo banco emissor.");

        result.IsSuccess.Should().BeTrue();
        pagamento.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.Recusado);
        pagamento.UltimoMotivoFalha.Should().Contain("Cartão recusado");
    }

    [Fact]
    public void Confirmar_NoEstadoPendenteGeracao_RetornaFalha()
    {
        var pagamento = new PagamentoBuilder().Build();

        var result = pagamento.Confirmar(PagamentoConfirmadoVia.Polling);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegistrarTentativaVerificacao_IncrementaContador()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();

        var first = pagamento.RegistrarTentativaVerificacao();
        var second = pagamento.RegistrarTentativaVerificacao();

        first.Data.Should().Be(1);
        second.Data.Should().Be(2);
        pagamento.TentativasVerificacao.Should().Be(2);
    }

    [Fact]
    public void Expirar_DepoisDeAguardandoConfirmacao_TransicionaParaExpirado()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();

        var result = pagamento.Expirar();

        result.IsSuccess.Should().BeTrue();
        pagamento.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.Expirado);
    }
}
