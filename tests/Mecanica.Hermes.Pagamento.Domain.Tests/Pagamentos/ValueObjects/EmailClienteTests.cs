using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.ValueObjects;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Pagamentos.ValueObjects;

public class EmailClienteTests
{
    [Fact]
    public void Criar_ComEmailValido_RetornaSucesso()
    {
        var result = EmailCliente.Criar("joao@example.com");

        result.IsSuccess.Should().BeTrue();
        result.Data!.Valor.Should().Be("joao@example.com");
    }

    [Fact]
    public void Criar_ComEmailEmMaiusculas_NormalizaParaMinusculas()
    {
        var result = EmailCliente.Criar("Joao@Example.COM");

        result.Data!.Valor.Should().Be("joao@example.com");
    }

    [Fact]
    public void Criar_ComEspacoEmBranco_FazTrim()
    {
        var result = EmailCliente.Criar("  joao@example.com  ");

        result.Data!.Valor.Should().Be("joao@example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Criar_ComValorVazioOuNulo_RetornaFalha(string? valor)
    {
        var result = EmailCliente.Criar(valor);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("obrigatório", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("nao-eh-email")]
    [InlineData("sem-arroba.com")]
    [InlineData("a@b")]
    [InlineData("@example.com")]
    [InlineData("joao@")]
    [InlineData("joao@@example.com")]
    [InlineData("joao @example.com")]
    public void Criar_ComFormatoInvalido_RetornaFalha(string valor)
    {
        var result = EmailCliente.Criar(valor);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Contains("inválido", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Construtor_Default_InicializaValorComoStringVazia()
    {
        var email = new EmailCliente();

        email.Valor.Should().Be(string.Empty);
    }
}
