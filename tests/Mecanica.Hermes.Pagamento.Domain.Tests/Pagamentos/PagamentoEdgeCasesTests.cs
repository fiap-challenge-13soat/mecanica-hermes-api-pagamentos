using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Events;
using Mecanica.Hermes.Pagamento.TestUtils.Builders;
using Xunit;
using PagamentoAggregate = Mecanica.Hermes.Pagamento.Domain.Pagamentos.Pagamento;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Pagamentos;

public class PagamentoEdgeCasesTests
{
    [Fact]
    public void Criar_ComIdVazio_RetornaFalha()
    {
        var result = PagamentoAggregate.Criar(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "joao@example.com",
            "Joao",
            10m);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("Id do pagamento", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Criar_ComOrdemDeServicoIdVazio_RetornaFalha()
    {
        var result = PagamentoAggregate.Criar(
            Guid.NewGuid(),
            Guid.Empty,
            Guid.NewGuid(),
            "joao@example.com",
            "Joao",
            10m);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("OrdemDeServico", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Criar_ComClienteIdVazio_RetornaFalha()
    {
        var result = PagamentoAggregate.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            "joao@example.com",
            "Joao",
            10m);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("ClienteId", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_ComNomeClienteVazio_RetornaFalha(string? nome)
    {
        var result = PagamentoAggregate.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "joao@example.com",
            nome,
            10m);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("nome do cliente", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_ComLimiteVerificacoesInvalido_RetornaFalha(int limite)
    {
        var result = PagamentoAggregate.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "joao@example.com",
            "Joao",
            10m,
            null,
            limite);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("limite de verificações", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Criar_ComMultiplosErros_AcumulaTodos()
    {
        var result = PagamentoAggregate.Criar(
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            null,
            null,
            -1m);

        result.IsFailure.Should().BeTrue();
        result.Errors.Count.Should().BeGreaterThan(3);
    }

    [Fact]
    public void Criar_FazTrimDoNomeCliente()
    {
        var result = PagamentoAggregate.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "joao@example.com",
            "  Joao  ",
            10m);

        result.IsSuccess.Should().BeTrue();
        result.Data!.NomeCliente.Should().Be("Joao");
    }

    [Fact]
    public void Criar_PublicaPagamentoCriadoEvent()
    {
        var pagamento = new PagamentoBuilder().Build();

        pagamento.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PagamentoCriadoEvent>();
    }

    [Fact]
    public void Criar_PreservaLimiteVerificacoesPersonalizado()
    {
        var pagamento = new PagamentoBuilder().WithLimiteVerificacoes(5).Build();

        pagamento.LimiteVerificacoes.Should().Be(5);
    }

    [Fact]
    public void Criar_DefiniLimiteVerificacoesDefault_QuandoOmitido()
    {
        var pagamento = new PagamentoBuilder().Build();

        pagamento.LimiteVerificacoes.Should().Be(PagamentoAggregate.LimiteVerificacoesDefault);
    }

    [Fact]
    public void Criar_GuardaVencimentoNuloPorDefault()
    {
        var result = PagamentoAggregate.Criar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "joao@example.com",
            "Joao",
            10m);

        result.Data!.Vencimento.Should().BeNull();
    }

    [Fact]
    public void GerarLink_ComUrlInvalida_RetornaFalhaSemAlterarStatus()
    {
        var pagamento = new PagamentoBuilder().Build();

        var result = pagamento.GerarLink("PREF-1", "url-invalida");

        result.IsFailure.Should().BeTrue();
        pagamento.StatusAtual.StatusAtual.Should().Be(PagamentoStatus.PendenteGeracao);
        pagamento.LinkPagamento.Should().BeNull();
    }

    [Fact]
    public void GerarLink_NoEstadoLinkGerado_RetornaFalha()
    {
        var pagamento = new PagamentoBuilder().Build();
        pagamento.GerarLink("PREF-1", "https://mp.example/pay");

        var result = pagamento.GerarLink("PREF-2", "https://mp.example/pay2");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GerarLink_ArquivaHistoricoComStatusAnteriorIgualAoSendoArquivado()
    {
        // Quando há transição do primeiro estado, o "StatusAnterior" do histórico
        // deve ser igual ao status sendo arquivado (PendenteGeracao), pois não
        // havia predecessor.
        var pagamento = new PagamentoBuilder().Build();

        pagamento.GerarLink("PREF-1", "https://mp.example/pay");

        pagamento.HistoricosStatus.Should().HaveCount(1);
        pagamento.HistoricosStatus.Single().StatusAnterior.Should().Be(PagamentoStatus.PendenteGeracao);
        pagamento.HistoricosStatus.Single().StatusAtual.Should().Be(PagamentoStatus.PendenteGeracao);
    }

    [Fact]
    public void MarcarEmailEnviado_AposGerarLink_ArquivaHistoricoComStatusAnteriorAnterior()
    {
        var pagamento = new PagamentoBuilder().Build();
        pagamento.GerarLink("PREF-1", "https://mp.example/pay");

        pagamento.MarcarEmailEnviado();

        pagamento.HistoricosStatus.Should().HaveCount(2);
        pagamento.HistoricosStatus.Last().StatusAnterior.Should().Be(PagamentoStatus.PendenteGeracao);
        pagamento.HistoricosStatus.Last().StatusAtual.Should().Be(PagamentoStatus.LinkGerado);
    }

    [Fact]
    public void MarcarEmailEnviado_NoEstadoPendenteGeracao_RetornaFalha()
    {
        var pagamento = new PagamentoBuilder().Build();

        var result = pagamento.MarcarEmailEnviado();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Recusar_ComMotivoVazio_RetornaFalha()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();

        var result = pagamento.Recusar("");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("motivo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Recusar_ComMotivoApenasEspacos_RetornaFalha()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();

        var result = pagamento.Recusar("   ");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Recusar_FazTrimDoMotivo()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();

        pagamento.Recusar("  motivo qualquer  ");

        pagamento.UltimoMotivoFalha.Should().Be("motivo qualquer");
    }

    [Fact]
    public void Expirar_NoEstadoPago_RetornaFalha()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.Confirmar(PagamentoConfirmadoVia.Webhook);

        var result = pagamento.Expirar();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegistrarTentativaVerificacao_NoEstadoTerminal_RetornaFalha()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.Confirmar(PagamentoConfirmadoVia.Webhook);

        var result = pagamento.RegistrarTentativaVerificacao();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void PodeReverificar_AbaixoDoLimite_RetornaTrue()
    {
        var pagamento = new PagamentoBuilder().WithLimiteVerificacoes(3).BuildAguardandoConfirmacao();
        pagamento.RegistrarTentativaVerificacao();

        pagamento.PodeReverificar.Should().BeTrue();
    }

    [Fact]
    public void PodeReverificar_AoAtingirLimite_RetornaFalse()
    {
        var pagamento = new PagamentoBuilder().WithLimiteVerificacoes(2).BuildAguardandoConfirmacao();
        pagamento.RegistrarTentativaVerificacao();
        pagamento.RegistrarTentativaVerificacao();

        pagamento.PodeReverificar.Should().BeFalse();
    }

    [Fact]
    public void PodeReverificar_NoEstadoTerminal_RetornaFalse()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.Confirmar(PagamentoConfirmadoVia.Webhook);

        pagamento.PodeReverificar.Should().BeFalse();
    }

    [Fact]
    public void Confirmar_PublicaPagamentoConfirmadoEvent()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.ClearDomainEvents();

        pagamento.Confirmar(PagamentoConfirmadoVia.Polling);

        pagamento.DomainEvents.Should().ContainSingle(e => e is PagamentoConfirmadoEvent);
    }

    [Fact]
    public void Recusar_PublicaPagamentoRecusadoEvent()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.ClearDomainEvents();

        pagamento.Recusar("motivo");

        pagamento.DomainEvents.Should().ContainSingle(e => e is PagamentoRecusadoEvent);
    }

    [Fact]
    public void Expirar_PublicaPagamentoExpiradoEvent()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();
        pagamento.ClearDomainEvents();

        pagamento.Expirar();

        pagamento.DomainEvents.Should().ContainSingle(e => e is PagamentoExpiradoEvent);
    }

    [Fact]
    public void GerarLink_PublicaLinkPagamentoGeradoEvent()
    {
        var pagamento = new PagamentoBuilder().Build();
        pagamento.ClearDomainEvents();

        pagamento.GerarLink("PREF-1", "https://mp.example/pay");

        pagamento.DomainEvents.Should().ContainSingle(e => e is LinkPagamentoGeradoEvent);
    }

    [Fact]
    public void Confirmar_AtualizaUpdatedAt()
    {
        var pagamento = new PagamentoBuilder().BuildAguardandoConfirmacao();

        pagamento.Confirmar(PagamentoConfirmadoVia.Webhook);

        pagamento.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void GerarLink_AtualizaUpdatedAt()
    {
        var pagamento = new PagamentoBuilder().Build();

        pagamento.GerarLink("PREF-1", "https://mp.example/pay");

        pagamento.UpdatedAt.Should().NotBeNull();
    }
}
