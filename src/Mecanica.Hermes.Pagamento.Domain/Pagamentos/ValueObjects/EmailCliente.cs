using System.Text.RegularExpressions;
using Mecanica.Hermes.Shared.Core.Results;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.ValueObjects;

/// <summary>
///     E-mail do cliente destinatário do link de pagamento. Validado por regex pragmática
///     (não RFC 5322 completa) — suficiente para o uso aqui (envio de e-mail transacional).
/// </summary>
public sealed partial class EmailCliente
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase, "pt-BR")]
    private static partial Regex EmailPattern();

    public EmailCliente()
    {
        // Constructor para o driver Mongo (deserialização)
        Valor = string.Empty;
    }

    private EmailCliente(string valor)
    {
        Valor = valor;
    }

    public string Valor { get; private set; }

    public override string ToString()
    {
        return Valor;
    }

    public static Result<EmailCliente> Criar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return Result<EmailCliente>.BadRequest("O e-mail do cliente é obrigatório.");

        var normalized = valor.Trim().ToLowerInvariant();
        if (!EmailPattern().IsMatch(normalized))
            return Result<EmailCliente>.BadRequest("E-mail do cliente inválido.");

        return Result<EmailCliente>.Ok(new EmailCliente(normalized));
    }
}
