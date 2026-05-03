using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Mappings;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Xunit;
using PagamentoAggregate = Mecanica.Hermes.Pagamento.Domain.Pagamentos.Pagamento;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.Persistence.Mappings;

public class PagamentoMapperTests
{
    [Fact]
    public void ToDocument_AguardandoConfirmacao_MapsAllScalarFields()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao(
            preferenceId: "PREF-1",
            url: "https://mp.example/pay/abc");

        var document = pagamento.ToDocument(version: 7);

        document.Id.Should().Be(pagamento.Id);
        document.OrdemDeServicoId.Should().Be(pagamento.OrdemDeServicoId);
        document.ClienteId.Should().Be(pagamento.ClienteId);
        document.EmailCliente.Should().Be(pagamento.EmailCliente.Valor);
        document.NomeCliente.Should().Be(pagamento.NomeCliente);
        document.Valor.Should().Be(pagamento.Valor.Valor);
        document.Moeda.Should().Be(pagamento.Valor.Moeda);
        document.Vencimento.Should().Be(pagamento.Vencimento);
        document.StatusAtual.Should().Be(PagamentoStatus.AguardandoConfirmacao.ToString());
        document.MercadoPagoPreferenceId.Should().Be("PREF-1");
        document.LinkPagamentoUrl.Should().Be("https://mp.example/pay/abc");
        document.LimiteVerificacoes.Should().Be(pagamento.LimiteVerificacoes);
        document.TentativasVerificacao.Should().Be(pagamento.TentativasVerificacao);
        document.CreatedAt.Should().Be(pagamento.CreatedAt);
        document.UpdatedAt.Should().Be(pagamento.UpdatedAt);
        document.Version.Should().Be(7);
        document.HistoricosStatus.Should().NotBeEmpty();
    }

    [Fact]
    public void ToDocument_PendenteGeracao_NoLinkOrConfirmadoVia()
    {
        var pagamento = new PagamentoBuilder().Build();

        var document = pagamento.ToDocument(version: 1);

        document.MercadoPagoPreferenceId.Should().BeNull();
        document.LinkPagamentoUrl.Should().BeNull();
        document.ConfirmadoVia.Should().BeNull();
        document.UltimoMotivoFalha.Should().BeNull();
        document.StatusAtual.Should().Be(PagamentoStatus.PendenteGeracao.ToString());
        document.HistoricosStatus.Should().BeEmpty();
    }

    [Fact]
    public void ToDocument_ThenToDomain_RoundTripsScalarsAndStatus()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao(
            preferenceId: "PREF-XYZ",
            url: "https://mp.example/pay/xyz");

        var document = pagamento.ToDocument(version: 2);
        var restored = document.ToDomain();

        restored.Id.Should().Be(pagamento.Id);
        restored.OrdemDeServicoId.Should().Be(pagamento.OrdemDeServicoId);
        restored.ClienteId.Should().Be(pagamento.ClienteId);
        restored.EmailCliente.Valor.Should().Be(pagamento.EmailCliente.Valor);
        restored.NomeCliente.Should().Be(pagamento.NomeCliente);
        restored.Valor.Valor.Should().Be(pagamento.Valor.Valor);
        restored.Valor.Moeda.Should().Be(pagamento.Valor.Moeda);
        restored.Vencimento.Should().Be(pagamento.Vencimento);
        restored.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.AguardandoConfirmacao);
        restored.LinkPagamento.Should().NotBeNull();
        restored.LinkPagamento!.PreferenceId.Should().Be("PREF-XYZ");
        restored.LinkPagamento!.Url.Should().Be("https://mp.example/pay/xyz");
        restored.LimiteVerificacoes.Should().Be(pagamento.LimiteVerificacoes);
    }

    [Fact]
    public void ToDocument_ThenToDomain_PreservesHistoricoEntries()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();

        var document = pagamento.ToDocument(version: 3);
        var restored = document.ToDomain();

        restored.HistoricosStatus.Should().HaveCount(pagamento.HistoricosStatus.Count);

        var originals = pagamento.HistoricosStatus.OrderBy(h => h.DataInicio).ToList();
        var restoredList = restored.HistoricosStatus.OrderBy(h => h.DataInicio).ToList();
        for (var i = 0; i < originals.Count; i++)
        {
            restoredList[i].StatusAtual.Should().Be(originals[i].StatusAtual);
            restoredList[i].StatusAnterior.Should().Be(originals[i].StatusAnterior);
            restoredList[i].StatusDestino.Should().Be(originals[i].StatusDestino);
            restoredList[i].DataInicio.Should().Be(originals[i].DataInicio);
            restoredList[i].DataFinalizacao.Should().Be(originals[i].DataFinalizacao);
        }
    }

    [Fact]
    public void ToDocument_HistoricoWithEmptyId_ReplacedWithNewGuid()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();

        var document = pagamento.ToDocument(version: 1);

        document.HistoricosStatus.Should().NotBeEmpty();
        document.HistoricosStatus.Should().OnlyContain(h => h.Id != Guid.Empty);
    }

    [Fact]
    public void ToDomain_AfterConfirmar_PreservesConfirmadoVia()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.Confirmar(PagamentoConfirmadoVia.Webhook);

        var document = pagamento.ToDocument(version: 4);
        var restored = document.ToDomain();

        restored.ConfirmadoVia.Should().Be(PagamentoConfirmadoVia.Webhook);
        restored.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.Pago);
    }

    [Fact]
    public void ToDomain_AfterRecusar_PreservesUltimoMotivoFalha()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.Recusar("cartão recusado");

        var document = pagamento.ToDocument(version: 5);
        var restored = document.ToDomain();

        restored.UltimoMotivoFalha.Should().Be("cartão recusado");
        restored.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.Recusado);
    }

    [Fact]
    public void ToDomain_AfterExpirar_StatusIsExpirado()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.Expirar();

        var document = pagamento.ToDocument(version: 6);
        var restored = document.ToDomain();

        restored.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.Expirado);
    }
}
