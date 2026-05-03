namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Constants;

/// <summary>
///     Chaves usadas em métricas de OpenTelemetry para operações sobre o agregado Pagamento.
///     Centralizadas para evitar drift entre Consumers e dashboards.
/// </summary>
public static class PagamentoMetricKeys
{
    public static class Operations
    {
        public const string GerarLink = "gerar_link";
        public const string ConfirmarWebhook = "confirmar_webhook";
        public const string ConfirmarPolling = "confirmar_polling";
        public const string Recusar = "recusar";
        public const string Expirar = "expirar";
        public const string VerificarStatus = "verificar_status";
        public const string DispatchEvent = "dispatch_event";
    }

    public static class ErrorTypes
    {
        public const string NotFound = "nao_encontrado";
        public const string InvalidStatus = "status_invalido";
        public const string DomainError = "domain_error";
        public const string ValidationError = "validacao";
        public const string ConcurrencyConflict = "concorrencia";
        public const string MercadoPagoError = "mercado_pago_error";
        public const string WebhookSignatureInvalid = "webhook_assinatura_invalida";
    }

    public static class WebhookOutcomes
    {
        public const string Approved = "approved";
        public const string Rejected = "rejected";
        public const string Pending = "pending";
        public const string Cancelled = "cancelled";
        public const string Duplicate = "duplicate";
    }
}
