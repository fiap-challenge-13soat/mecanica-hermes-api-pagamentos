using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Pagamentos;

public class PagamentoHistoricoStatusTests
{
    [Fact]
    public void HistoricoArquivado_AposGerarLink_PreservaStatusAtualEDataInicio()
    {
        var pagamento = new PagamentoBuilder().Build();
        var dataInicioOriginal = pagamento.StatusAtual.DataInicio;

        pagamento.GerarLink("PREF-1", "https://mp.example/pay");

        var historico = pagamento.HistoricosStatus.Should().ContainSingle().Subject;
        historico.StatusAtual.Should().Be(PagamentoStatus.PendenteGeracao);
        historico.DataInicio.Should().Be(dataInicioOriginal);
        historico.DataFinalizacao.Should().NotBeNull();
        historico.StatusDestino.Should().Be(PagamentoStatus.LinkGerado);
    }

    [Fact]
    public void HistoricoArquivado_PrimeiraTransicao_TemStatusAnteriorIgualAoArquivado()
    {
        var pagamento = new PagamentoBuilder().Build();

        pagamento.GerarLink("PREF-1", "https://mp.example/pay");

        var historico = pagamento.HistoricosStatus.Single();
        historico.StatusAnterior.Should().Be(PagamentoStatus.PendenteGeracao);
    }

    [Fact]
    public void HistoricoArquivado_SegundaTransicao_TemStatusAnteriorIgualAoStatusAtualAnterior()
    {
        var pagamento = new PagamentoBuilder().Build();
        pagamento.GerarLink("PREF-1", "https://mp.example/pay");

        pagamento.MarcarEmailEnviado();

        pagamento.HistoricosStatus.Should().HaveCount(2);
        var ultimo = pagamento.HistoricosStatus.Last();
        ultimo.StatusAnterior.Should().Be(PagamentoStatus.PendenteGeracao);
        ultimo.StatusAtual.Should().Be(PagamentoStatus.LinkGerado);
    }

    [Fact]
    public void HistoricoArquivado_HerdaIdEAtributosDaBase()
    {
        var pagamento = new PagamentoBuilder().Build();

        pagamento.GerarLink("PREF-1", "https://mp.example/pay");

        var historico = pagamento.HistoricosStatus.Single();
        historico.Id.Should().Be(Guid.Empty); // sem Id externo, BaseDomain default-construct vai com Empty
        historico.CreatedAt.Should().NotBe(default);
    }
}
