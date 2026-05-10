using System.Text.Json.Serialization;

namespace Mecanica.Hermes.Pagamento.Api.Endpoints.Webhooks.Contracts;

/// <summary>
///     Payload simplificado do webhook do Mercado Pago. O MP envia
///     <c>{ "type": "payment", "data": { "id": "..." } }</c> com vários extras opcionais; aqui só
///     extraímos o necessário para resolver <c>PagamentoId</c> via external_reference.
/// </summary>
public sealed class MercadoPagoWebhookPayload
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("action")] public string? Action { get; set; }
    [JsonPropertyName("data")] public MercadoPagoWebhookData? Data { get; set; }
}

public sealed class MercadoPagoWebhookData
{
    [JsonPropertyName("id")] public string? Id { get; set; }
}
