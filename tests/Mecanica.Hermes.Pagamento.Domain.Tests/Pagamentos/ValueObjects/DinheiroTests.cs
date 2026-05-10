using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.ValueObjects;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Pagamentos.ValueObjects;

public class DinheiroTests
{
    [Fact]
    public void Criar_ComValorValido_RetornaSucesso()
    {
        var result = Dinheiro.Criar(10.50m);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Valor.Should().Be(10.50m);
        result.Data.Moeda.Should().Be("BRL");
    }

    [Fact]
    public void Criar_SemMoedaExplicita_UsaMoedaPadraoBRL()
    {
        var result = Dinheiro.Criar(1m);

        result.Data!.Moeda.Should().Be(Dinheiro.MoedaPadrao);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_ComMoedaVaziaOuNula_RetornaFalha(string? moeda)
    {
        var result = Dinheiro.Criar(10m, moeda!);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("moeda", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.001)]
    [InlineData(0.009)]
    [InlineData(-1)]
    public void Criar_ComValorAbaixoDeUmCentavo_RetornaFalha(decimal valor)
    {
        var result = Dinheiro.Criar(valor);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("0,01", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Criar_ComValorMinimo_RetornaSucesso()
    {
        var result = Dinheiro.Criar(0.01m);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Valor.Should().Be(0.01m);
    }

    [Fact]
    public void Criar_TruncaCasasDecimaisAlemDeCentavos()
    {
        var result = Dinheiro.Criar(10.999m);

        result.Data!.Valor.Should().Be(10.99m);
    }

    [Fact]
    public void Criar_TruncaSemArredondarParaCima()
    {
        // 10.995 -> 10.99 (truncamento, não arredondamento)
        var result = Dinheiro.Criar(10.995m);

        result.Data!.Valor.Should().Be(10.99m);
    }

    [Fact]
    public void Criar_NormalizaMoedaParaMaiusculasInvariantes()
    {
        var result = Dinheiro.Criar(10m, "usd");

        result.Data!.Moeda.Should().Be("USD");
    }

    [Fact]
    public void Construtor_Default_UsaMoedaPadrao()
    {
        var dinheiro = new Dinheiro();

        dinheiro.Moeda.Should().Be(Dinheiro.MoedaPadrao);
    }
}
