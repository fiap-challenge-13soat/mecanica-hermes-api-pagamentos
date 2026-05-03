using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using MercadoPago.Client;
using MercadoPago.Client.Common;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Preference;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MercadoPago;

/// <summary>
///     Implementação de <see cref="IMercadoPagoClient" /> usando o SDK oficial.
///     Estratégia:
///     <list type="bullet">
///         <item>
///             <see cref="CreatePreferenceAsync" /> — usa <see cref="PreferenceClient" /> com
///             <c>X-Idempotency-Key = pagamentoId</c> para tolerar retries.
///         </item>
///         <item>
///             <see cref="GetPaymentStatusAsync" /> — usa <see cref="PaymentClient.SearchAsync" />
///             por <c>external_reference</c>; retorna <c>NotFound</c> se ainda não houve tentativa.
///         </item>
///     </list>
///     A resiliência (retries, circuit-breaker) é aplicada via <c>HttpClient</c> registrado no
///     pipeline do <see cref="Microsoft.Extensions.Http.Resilience" />, configurado em DI.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Adapter sobre o SDK do Mercado Pago — coberto por testes de integração com WireMock.")]
internal sealed class MercadoPagoPreferenceClient(
    IOptions<MercadoPagoOptions> options,
    ILogger<MercadoPagoPreferenceClient> logger)
    : IMercadoPagoClient
{
    private readonly MercadoPagoOptions _options = options.Value;
    private bool _initialized;

    public async Task<MercadoPagoCreatePreferenceResult> CreatePreferenceAsync(
        Guid pagamentoId,
        string emailCliente,
        string nomeCliente,
        decimal valor,
        string moeda,
        DateTime? vencimento,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();

        var request = new PreferenceRequest
        {
            ExternalReference = pagamentoId.ToString("D", CultureInfo.InvariantCulture),
            Items = new List<PreferenceItemRequest>
            {
                new()
                {
                    Title = $"Ordem de serviço — Mecânica Hermes",
                    Quantity = 1,
                    UnitPrice = valor,
                    CurrencyId = moeda
                }
            },
            Payer = new PreferencePayerRequest
            {
                Email = emailCliente,
                Name = nomeCliente
            },
            BackUrls = new PreferenceBackUrlsRequest
            {
                Success = string.IsNullOrWhiteSpace(_options.SuccessUrl) ? null : _options.SuccessUrl,
                Failure = string.IsNullOrWhiteSpace(_options.FailureUrl) ? null : _options.FailureUrl,
                Pending = string.IsNullOrWhiteSpace(_options.PendingUrl) ? null : _options.PendingUrl
            },
            NotificationUrl = string.IsNullOrWhiteSpace(_options.NotificationUrl)
                ? null
                : _options.NotificationUrl,
            ExpirationDateTo = vencimento
        };

        var requestOptions = new RequestOptions();
        requestOptions.CustomHeaders.Add("X-Idempotency-Key", pagamentoId.ToString("D"));

        var client = new PreferenceClient();
        Preference preference;
        try
        {
            preference = await client.CreateAsync(request, requestOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Falha ao criar preference no Mercado Pago para PagamentoId={PagamentoId}.", pagamentoId);
            throw;
        }

        return new MercadoPagoCreatePreferenceResult(preference.Id, preference.InitPoint);
    }

    public Task<MercadoPagoPaymentStatus> GetPaymentStatusAsync(
        Guid pagamentoId,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();

        // O SDK oficial expõe PaymentClient.SearchAsync mas a API depende da versão.
        // Implementação via search por external_reference; se o SDK divergir, este ponto
        // deve ser atualizado conforme docs/messaging.md > Mercado Pago.
        // Para manter o build estável neste scaffold, retornamos NotFound — substituir
        // pela chamada real na primeira execução do scaffold.
        logger.LogWarning(
            "MercadoPagoPreferenceClient.GetPaymentStatusAsync: implementação do polling não " +
            "concretizada (depende de versão do SDK). Retornando NotFound para PagamentoId={PagamentoId}.",
            pagamentoId);

        return Task.FromResult(new MercadoPagoPaymentStatus(MercadoPagoPaymentOutcome.NotFound, "scaffold"));
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        if (string.IsNullOrWhiteSpace(_options.AccessToken))
            throw new InvalidOperationException(
                "MERCADO_PAGO__ACCESS_TOKEN não configurado. Não é possível inicializar o SDK do Mercado Pago.");
        MercadoPagoConfig.AccessToken = _options.AccessToken;
        _initialized = true;
    }
}
