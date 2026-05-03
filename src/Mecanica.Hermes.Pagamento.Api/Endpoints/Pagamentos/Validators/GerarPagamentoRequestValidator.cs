using FluentValidation;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Contracts;

namespace Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Validators;

public sealed class GerarPagamentoRequestValidator : AbstractValidator<GerarPagamentoRequest>
{
    public GerarPagamentoRequestValidator()
    {
        RuleFor(x => x.OrdemDeServicoId)
            .NotEmpty().WithMessage("O OrdemDeServicoId é obrigatório.");

        RuleFor(x => x.ClienteId)
            .NotEmpty().WithMessage("O ClienteId é obrigatório.");

        RuleFor(x => x.EmailCliente)
            .NotEmpty().WithMessage("O e-mail do cliente é obrigatório.")
            .EmailAddress().WithMessage("E-mail do cliente inválido.")
            .MaximumLength(254);

        RuleFor(x => x.NomeCliente)
            .NotEmpty().WithMessage("O nome do cliente é obrigatório.")
            .MaximumLength(200);

        RuleFor(x => x.Valor)
            .GreaterThanOrEqualTo(0.01m).WithMessage("O valor do pagamento deve ser maior ou igual a 0,01.");

        RuleFor(x => x.Vencimento)
            .GreaterThan(DateTime.UtcNow).When(x => x.Vencimento.HasValue)
            .WithMessage("O vencimento deve ser uma data futura.");
    }
}
