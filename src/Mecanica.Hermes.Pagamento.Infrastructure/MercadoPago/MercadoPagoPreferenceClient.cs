using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MercadoPago;

/// <summary>
///     Implementação de <see cref="IMercadoPagoClient" /> usando <see cref="HttpClient" /> direto.
///     A <c>BaseAddress</c> é configurada via <c>MERCADO_PAGO__API_BASE_URL</c> (default:
///     <c>https://api.mercadopago.com</c>), o que permite apontar para WireMock em testes E2E.
/// </summary>
[ExcludeFromCodeCoverage(Justification =
    "Adapter HTTP sobre a API do Mercado Pago — coberto por testes de integração com WireMock.")]
internal sealed partial class MercadoPagoPreferenceClient(
    HttpClient httpClient,
    IOptions<MercadoPagoOptions> options,
    ILogger<MercadoPagoPreferenceClient> logger)
    : IMercadoPagoClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly MercadoPagoOptions _options = options.Value;

    public async Task<MercadoPagoCreatePreferenceResult> CreatePreferenceAsync(
        Guid pagamentoId,
        string emailCliente,
        string nomeCliente,
        decimal valor,
        string moeda,
        DateTime? vencimento,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            external_reference = pagamentoId.ToString("D", CultureInfo.InvariantCulture),
            items = new[]
            {
                new
                {
                    title = "Ordem de serviço — Mecânica Hermes",
                    quantity = 1,
                    unit_price = valor,
                    currency_id = moeda
                }
            },
            payer = new { email = emailCliente, first_name = nomeCliente },
            back_urls = new
            {
                success = _options.SuccessUrl,
                failure = _options.FailureUrl,
                pending = _options.PendingUrl
            },
            notification_url = _options.NotificationUrl,
            expiration_date_to = vencimento?.ToString("o", CultureInfo.InvariantCulture)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "checkout/preferences");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);
        request.Headers.Add("X-Idempotency-Key", pagamentoId.ToString("D"));
        request.Content = JsonContent.Create(body, options: JsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            LogFalhaCriarPreference(logger, ex, pagamentoId);
            throw;
        }

        var result = await response.Content.ReadFromJsonAsync<MpPreferenceResponse>(JsonOptions, cancellationToken);
        return new MercadoPagoCreatePreferenceResult(result!.Id, result.InitPoint);
    }

    public async Task<MercadoPagoPaymentStatus> GetPaymentStatusAsync(
        Guid pagamentoId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"v1/payments/search?external_reference={pagamentoId:D}");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            LogFalhaConsultarStatus(logger, ex, pagamentoId);
            return new MercadoPagoPaymentStatus(MercadoPagoPaymentOutcome.NotFound, "error");
        }

        var result = await response.Content.ReadFromJsonAsync<MpSearchResponse>(JsonOptions, cancellationToken);

        var first = result?.Results?.FirstOrDefault();
        if (first is null)
            return new MercadoPagoPaymentStatus(MercadoPagoPaymentOutcome.NotFound, "not_found");

        var outcome = first.Status switch
        {
            "approved" => MercadoPagoPaymentOutcome.Approved,
            "rejected" or "cc_rejected" => MercadoPagoPaymentOutcome.Rejected,
            "cancelled" => MercadoPagoPaymentOutcome.Cancelled,
            "expired" => MercadoPagoPaymentOutcome.Expired,
            _ => MercadoPagoPaymentOutcome.Pending
        };

        return new MercadoPagoPaymentStatus(outcome, first.Status);
    }

    private sealed record MpPreferenceResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("init_point")] string InitPoint);

    private sealed record MpSearchResponse(
        [property: JsonPropertyName("results")] List<MpPaymentResult>? Results);

    private sealed record MpPaymentResult(
        [property: JsonPropertyName("status")] string Status);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Falha ao criar preference no Mercado Pago para PagamentoId={PagamentoId}.")]
    private static partial void LogFalhaCriarPreference(ILogger logger, Exception exception, Guid pagamentoId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Falha ao consultar status no Mercado Pago para PagamentoId={PagamentoId}.")]
    private static partial void LogFalhaConsultarStatus(ILogger logger, Exception exception, Guid pagamentoId);
}
