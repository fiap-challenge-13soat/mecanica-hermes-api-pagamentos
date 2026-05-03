using Mecanica.Hermes.Pagamento.Domain.Common.Results;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.ValueObjects;

/// <summary>
///     Encapsula o resultado da criação da preference no Mercado Pago: o id da preference e
///     a URL pública (init_point) que deve ser enviada ao cliente.
/// </summary>
public sealed class LinkPagamento
{
    public LinkPagamento()
    {
        // Constructor para o driver Mongo (deserialização)
        PreferenceId = string.Empty;
        Url = string.Empty;
    }

    private LinkPagamento(string preferenceId, string url)
    {
        PreferenceId = preferenceId;
        Url = url;
    }

    public string PreferenceId { get; private set; }
    public string Url { get; private set; }

    public static Result<LinkPagamento> Criar(string? preferenceId, string? url)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(preferenceId))
            errors.Add("O preferenceId do Mercado Pago é obrigatório.");

        if (string.IsNullOrWhiteSpace(url))
            errors.Add("A URL do link de pagamento é obrigatória.");
        else if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                 (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            errors.Add("A URL do link de pagamento precisa ser absoluta (http/https).");

        if (errors.Count > 0)
            return Result<LinkPagamento>.BadRequest(errors);

        return Result<LinkPagamento>.Ok(new LinkPagamento(preferenceId!.Trim(), url!.Trim()));
    }
}
