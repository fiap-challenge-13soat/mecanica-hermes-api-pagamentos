using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MercadoPago;

/// <summary>
///     Valida o cabeçalho <c>x-signature</c> do webhook do Mercado Pago. O cabeçalho
///     contém um payload no formato <c>ts=...,v1=...</c>; a assinatura HMAC-SHA256 é
///     calculada sobre <c>id:{dataId};request-id:{requestId};ts:{ts};</c> usando o
///     <c>WebhookSecret</c> configurado.
/// </summary>
public sealed class WebhookSignatureValidator(IOptions<MercadoPagoOptions> options)
{
    private readonly MercadoPagoOptions _options = options.Value;

    /// <summary>
    ///     Valida a assinatura. Retorna <c>true</c> apenas quando o HMAC bate com o
    ///     <c>WebhookSecret</c> configurado. Sem secret configurado a validação SEMPRE
    ///     falha (fail-closed) — não há bypass automático para Development.
    /// </summary>
    public bool Validate(
        string? signatureHeader,
        string? requestIdHeader,
        string dataId)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
            // Sem secret configurado: rejeita para não expor o endpoint a forgery em produção.
            // Em DEV/Testing, defina MERCADO_PAGO__WEBHOOK_SECRET=dev-secret no launchSettings local.
            return false;

        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(dataId))
            return false;

        var (ts, v1) = ParseSignatureHeader(signatureHeader);
        if (ts is null || v1 is null) return false;

        var manifest = $"id:{dataId};request-id:{requestIdHeader ?? string.Empty};ts:{ts};";
        var hmac = ComputeHmacSha256(manifest, _options.WebhookSecret);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(hmac.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(v1.ToLowerInvariant()));
    }

    private static (string? ts, string? v1) ParseSignatureHeader(string header)
    {
        string? ts = null, v1 = null;
        foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=', StringComparison.Ordinal);
            if (idx <= 0) continue;
            var key = part[..idx].Trim();
            var value = part[(idx + 1)..].Trim();
            if (string.Equals(key, "ts", StringComparison.OrdinalIgnoreCase)) ts = value;
            else if (string.Equals(key, "v1", StringComparison.OrdinalIgnoreCase)) v1 = value;
        }
        return (ts, v1);
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
