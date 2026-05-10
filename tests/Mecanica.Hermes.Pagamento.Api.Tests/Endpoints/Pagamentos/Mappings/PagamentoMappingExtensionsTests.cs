using FluentAssertions;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Mappings;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Api.Tests.Endpoints.Pagamentos.Mappings;

public class PagamentoMappingExtensionsTests
{
    [Fact]
    public void ToResponse_MapsAllPrimitiveFields()
    {
        var dto = BuildDto();

        var response = dto.ToResponse();

        response.Id.Should().Be(dto.Id);
        response.OrdemDeServicoId.Should().Be(dto.OrdemDeServicoId);
        response.ClienteId.Should().Be(dto.ClienteId);
        response.EmailCliente.Should().Be(dto.EmailCliente);
        response.NomeCliente.Should().Be(dto.NomeCliente);
        response.Valor.Should().Be(dto.Valor);
        response.Moeda.Should().Be(dto.Moeda);
        response.StatusAtual.Should().Be(dto.StatusAtual);
        response.UltimoMotivoFalha.Should().Be(dto.UltimoMotivoFalha);
        response.ConfirmadoVia.Should().Be(dto.ConfirmadoVia);
        response.TentativasVerificacao.Should().Be(dto.TentativasVerificacao);
        response.LimiteVerificacoes.Should().Be(dto.LimiteVerificacoes);
        response.Vencimento.Should().Be(dto.Vencimento);
        response.CreatedAt.Should().Be(dto.CreatedAt);
        response.UpdatedAt.Should().Be(dto.UpdatedAt);
    }

    [Fact]
    public void ToResponse_MapsLinkPagamento_WhenPresent()
    {
        var dto = BuildDto(link: new LinkPagamentoDto("pref-123", "https://mp.example/pay"));

        var response = dto.ToResponse();

        response.LinkPagamento.Should().NotBeNull();
        response.LinkPagamento!.PreferenceId.Should().Be("pref-123");
        response.LinkPagamento.Url.Should().Be("https://mp.example/pay");
    }

    [Fact]
    public void ToResponse_MapsLinkPagamento_AsNull_WhenAbsent()
    {
        var dto = BuildDto(link: null);

        var response = dto.ToResponse();

        response.LinkPagamento.Should().BeNull();
    }

    [Fact]
    public void ToResponse_MapsHistoricos_PreservingOrderAndCount()
    {
        var hist1 = new PagamentoHistoricoStatusDto(
            Guid.NewGuid(), "PendenteGeracao", "PendenteGeracao", "LinkGerado",
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5));
        var hist2 = new PagamentoHistoricoStatusDto(
            Guid.NewGuid(), "LinkGerado", "LinkGerado", "AguardandoConfirmacao",
            DateTime.UtcNow.AddMinutes(-5), null);

        var dto = BuildDto(historicos: new List<PagamentoHistoricoStatusDto> { hist1, hist2 });

        var response = dto.ToResponse();

        response.HistoricosStatus.Should().HaveCount(2);
        response.HistoricosStatus[0].Id.Should().Be(hist1.Id);
        response.HistoricosStatus[0].StatusDestino.Should().Be("LinkGerado");
        response.HistoricosStatus[1].Id.Should().Be(hist2.Id);
        response.HistoricosStatus[1].DataFinalizacao.Should().BeNull();
    }

    [Fact]
    public void ToResponse_WithEmptyHistoricos_ReturnsEmptyList()
    {
        var dto = BuildDto(historicos: new List<PagamentoHistoricoStatusDto>());

        var response = dto.ToResponse();

        response.HistoricosStatus.Should().BeEmpty();
    }

    private static PagamentoDto BuildDto(
        LinkPagamentoDto? link = null,
        IList<PagamentoHistoricoStatusDto>? historicos = null)
    {
        return new PagamentoDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "cliente@example.com",
            "Joao da Silva",
            199.90m,
            "BRL",
            "PendenteGeracao",
            link,
            null,
            null,
            0,
            15,
            DateTime.UtcNow.AddDays(7),
            historicos ?? new List<PagamentoHistoricoStatusDto>(),
            DateTime.UtcNow,
            null);
    }
}
