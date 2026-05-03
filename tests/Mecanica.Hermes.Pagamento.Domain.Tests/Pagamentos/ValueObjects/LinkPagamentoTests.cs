using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.ValueObjects;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Pagamentos.ValueObjects;

public class LinkPagamentoTests
{
    [Fact]
    public void Criar_ComDadosValidos_RetornaSucesso()
    {
        var result = LinkPagamento.Criar("PREF-1", "https://mp.example/pay");

        result.IsSuccess.Should().BeTrue();
        result.Data!.PreferenceId.Should().Be("PREF-1");
        result.Data.Url.Should().Be("https://mp.example/pay");
    }

    [Fact]
    public void Criar_FazTrimDosCampos()
    {
        var result = LinkPagamento.Criar("  PREF-1  ", "  https://mp.example/pay  ");

        result.Data!.PreferenceId.Should().Be("PREF-1");
        result.Data.Url.Should().Be("https://mp.example/pay");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_ComPreferenceIdInvalido_RetornaFalha(string? preferenceId)
    {
        var result = LinkPagamento.Criar(preferenceId, "https://mp.example/pay");

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("preferenceId", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_ComUrlVaziaOuNula_RetornaFalha(string? url)
    {
        var result = LinkPagamento.Criar("PREF-1", url);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("URL", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("nao-eh-url")]
    [InlineData("ftp://example.com/path")]
    [InlineData("//example.com/path")]
    [InlineData("file:///etc/passwd")]
    public void Criar_ComUrlInvalidaOuSemHttp_RetornaFalha(string url)
    {
        var result = LinkPagamento.Criar("PREF-1", url);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("absoluta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Criar_AceitaHttp()
    {
        var result = LinkPagamento.Criar("PREF-1", "http://mp.example/pay");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Criar_ComMultiplosErros_AcumulaTodos()
    {
        var result = LinkPagamento.Criar(null, null);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Construtor_Default_InicializaCamposComoStringVazia()
    {
        var link = new LinkPagamento();

        link.PreferenceId.Should().Be(string.Empty);
        link.Url.Should().Be(string.Empty);
    }
}
