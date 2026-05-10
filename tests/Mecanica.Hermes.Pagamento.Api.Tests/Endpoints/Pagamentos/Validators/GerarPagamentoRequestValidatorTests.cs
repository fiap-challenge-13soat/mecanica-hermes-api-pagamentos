using FluentAssertions;
using FluentValidation.TestHelper;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Contracts;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Validators;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Api.Tests.Endpoints.Pagamentos.Validators;

public class GerarPagamentoRequestValidatorTests
{
    private static GerarPagamentoRequest ValidRequest(Action<GerarPagamentoRequestBuilder>? configure = null)
    {
        var builder = new GerarPagamentoRequestBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }

    private sealed class GerarPagamentoRequestBuilder
    {
        public Guid OrdemDeServicoId { get; set; } = Guid.NewGuid();
        public Guid ClienteId { get; set; } = Guid.NewGuid();
        public string EmailCliente { get; set; } = "cliente@example.com";
        public string NomeCliente { get; set; } = "Joao da Silva";
        public decimal Valor { get; set; } = 199.90m;
        public DateTime? Vencimento { get; set; } = DateTime.UtcNow.AddDays(7);

        public GerarPagamentoRequest Build()
        {
            return new GerarPagamentoRequest(
                OrdemDeServicoId, ClienteId, EmailCliente, NomeCliente, Valor, Vencimento);
        }
    }

    private readonly GerarPagamentoRequestValidator _validator = new();

    [Fact]
    public async Task Validate_HappyPath_HasNoErrors()
    {
        var result = await _validator.TestValidateAsync(ValidRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_OrdemDeServicoIdEmpty_HasError()
    {
        var result = await _validator.TestValidateAsync(ValidRequest(b => b.OrdemDeServicoId = Guid.Empty));

        result.ShouldHaveValidationErrorFor(r => r.OrdemDeServicoId);
    }

    [Fact]
    public async Task Validate_ClienteIdEmpty_HasError()
    {
        var result = await _validator.TestValidateAsync(ValidRequest(b => b.ClienteId = Guid.Empty));

        result.ShouldHaveValidationErrorFor(r => r.ClienteId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao-eh-email")]
    [InlineData("@invalido")]
    public async Task Validate_EmailInvalido_HasError(string email)
    {
        var result = await _validator.TestValidateAsync(ValidRequest(b => b.EmailCliente = email));

        result.ShouldHaveValidationErrorFor(r => r.EmailCliente);
    }

    [Fact]
    public async Task Validate_EmailMuitoLongo_HasError()
    {
        var longEmail = new string('a', 250) + "@x.com";
        var result = await _validator.TestValidateAsync(ValidRequest(b => b.EmailCliente = longEmail));

        result.ShouldHaveValidationErrorFor(r => r.EmailCliente);
    }

    [Fact]
    public async Task Validate_NomeVazio_HasError()
    {
        var result = await _validator.TestValidateAsync(ValidRequest(b => b.NomeCliente = ""));

        result.ShouldHaveValidationErrorFor(r => r.NomeCliente);
    }

    [Fact]
    public async Task Validate_NomeMuitoLongo_HasError()
    {
        var nome = new string('a', 201);
        var result = await _validator.TestValidateAsync(ValidRequest(b => b.NomeCliente = nome));

        result.ShouldHaveValidationErrorFor(r => r.NomeCliente);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.009)]
    public async Task Validate_ValorInvalido_HasError(decimal valor)
    {
        var result = await _validator.TestValidateAsync(ValidRequest(b => b.Valor = valor));

        result.ShouldHaveValidationErrorFor(r => r.Valor);
    }

    [Fact]
    public async Task Validate_ValorMinimo_DoesNotHaveError()
    {
        var result = await _validator.TestValidateAsync(ValidRequest(b => b.Valor = 0.01m));

        result.ShouldNotHaveValidationErrorFor(r => r.Valor);
    }

    [Fact]
    public async Task Validate_VencimentoNoPassado_HasError()
    {
        var result = await _validator.TestValidateAsync(
            ValidRequest(b => b.Vencimento = DateTime.UtcNow.AddDays(-1)));

        result.ShouldHaveValidationErrorFor(r => r.Vencimento);
    }

    [Fact]
    public async Task Validate_VencimentoNulo_DoesNotHaveError()
    {
        var result = await _validator.TestValidateAsync(ValidRequest(b => b.Vencimento = null));

        result.ShouldNotHaveValidationErrorFor(r => r.Vencimento);
    }

    [Fact]
    public async Task Validate_RetornaTodosErros_QuandoVariosCamposInvalidos()
    {
        var request = ValidRequest(b =>
        {
            b.OrdemDeServicoId = Guid.Empty;
            b.ClienteId = Guid.Empty;
            b.EmailCliente = "ruim";
            b.NomeCliente = "";
            b.Valor = 0;
        });

        var result = await _validator.TestValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(3);
    }
}
