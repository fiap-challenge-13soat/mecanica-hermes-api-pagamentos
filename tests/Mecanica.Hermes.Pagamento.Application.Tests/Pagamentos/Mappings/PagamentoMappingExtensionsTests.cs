using FluentAssertions;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Mappings;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Pagamentos.Mappings;

public class PagamentoMappingExtensionsTests
{
    [Fact]
    public void ToDto_PendenteGeracao_MapeiaCamposBasicos()
    {
        var pagamento = new PagamentoBuilder()
            .WithEmail("teste@example.com")
            .WithNome("Cliente Teste")
            .WithValor(150.55m)
            .Build();

        var dto = pagamento.ToDto();

        dto.Id.Should().Be(pagamento.Id);
        dto.OrdemDeServicoId.Should().Be(pagamento.OrdemDeServicoId);
        dto.ClienteId.Should().Be(pagamento.ClienteId);
        dto.EmailCliente.Should().Be(pagamento.EmailCliente.Valor);
        dto.NomeCliente.Should().Be("Cliente Teste");
        dto.Valor.Should().Be(pagamento.Valor.Valor);
        dto.Moeda.Should().Be(pagamento.Valor.Moeda);
        dto.StatusAtual.Should().Be(PagamentoStatus.PendenteGeracao.ToString());
        dto.LinkPagamento.Should().BeNull();
        dto.UltimoMotivoFalha.Should().BeNull();
        dto.ConfirmadoVia.Should().BeNull();
        dto.TentativasVerificacao.Should().Be(0);
        dto.LimiteVerificacoes.Should().Be(pagamento.LimiteVerificacoes);
        dto.HistoricosStatus.Should().BeEmpty();
    }

    [Fact]
    public void ToDto_AguardandoConfirmacao_PreencheLinkEHistorico()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao("PREF-1", "https://mp/pay/1");

        var dto = pagamento.ToDto();

        dto.StatusAtual.Should().Be(PagamentoStatus.AguardandoConfirmacao.ToString());
        dto.LinkPagamento.Should().NotBeNull();
        dto.LinkPagamento!.PreferenceId.Should().Be("PREF-1");
        dto.LinkPagamento.Url.Should().Be("https://mp/pay/1");
        dto.HistoricosStatus.Should().NotBeEmpty();
    }

    [Fact]
    public void ToDto_PagamentoConfirmado_PreencheConfirmadoVia()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.Confirmar(PagamentoConfirmadoVia.Webhook);

        var dto = pagamento.ToDto();

        dto.StatusAtual.Should().Be(PagamentoStatus.Pago.ToString());
        dto.ConfirmadoVia.Should().Be(PagamentoConfirmadoVia.Webhook.ToString());
    }

    [Fact]
    public void ToDto_PagamentoRecusado_PreencheUltimoMotivoFalha()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.Recusar("cartao_invalido");

        var dto = pagamento.ToDto();

        dto.StatusAtual.Should().Be(PagamentoStatus.Recusado.ToString());
        dto.UltimoMotivoFalha.Should().Be("cartao_invalido");
    }
}
