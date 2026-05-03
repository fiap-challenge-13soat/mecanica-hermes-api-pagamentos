namespace Mecanica.Hermes.Pagamento.Infrastructure.MercadoPago;

public sealed class MercadoPagoOptions
{
    public string AccessToken { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string NotificationUrl { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = string.Empty;
    public string FailureUrl { get; set; } = string.Empty;
    public string PendingUrl { get; set; } = string.Empty;
}
