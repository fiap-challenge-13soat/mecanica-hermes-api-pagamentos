namespace Mecanica.Hermes.Pagamento.Infrastructure.Settings;

internal static class VariableNames
{
    public const string MediatRLicenseKey = "MediatR__LicenseKey";

    public const string AuthAuthority = "AUTH__AUTHORITY";
    public const string AuthIssuer = "AUTH__ISSUER";
    public const string AuthClienteScope = "AUTH__CLIENTE_SCOPE";
    public const string AuthAdminScope = "AUTH__ADMIN_SCOPE";

    public const string ObservabilityEnabled = "OBSERVABILITY__ENABLED";

    // OpenTelemetry
    public const string OtelServiceName = "OTEL_SERVICE_NAME";
    public const string OtelExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string OtelExporterOtlpHeaders = "OTEL_EXPORTER_OTLP_HEADERS";
    public const string OtelExporterOtlpProtocol = "OTEL_EXPORTER_OTLP_PROTOCOL";
    public const string OtelExporterOtlpLogsEndpoint = "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT";
    public const string OtelExporterOtlpLogsHeaders = "OTEL_EXPORTER_OTLP_LOGS_HEADERS";
    public const string OtelExporterOtlpLogsProtocol = "OTEL_EXPORTER_OTLP_LOGS_PROTOCOL";

    // MassTransit
    public const string MassTransitConnectionString = "MASSTRANSIT__CONNECTION_STRING";
    public const string MassTransitPrefetchCount = "MASSTRANSIT__PREFETCH_COUNT";
    public const string MassTransitTransportService = "MASSTRANSIT__TRANSPORT_SERVICE";
    public const string MassTransitQueueNamePrefix = "MASSTRANSIT__QUEUE_NAME_PREFIX";

    // MongoDB — agregados + saga + outbox + webhooks
    public const string MongoDbConnectionString = "MONGODB__CONNECTION_STRING";
    public const string MongoDbDatabase = "MONGODB__DATABASE";
    public const string MongoDbSagaDatabase = "MONGODB__SAGA_DATABASE";

    // Mercado Pago
    public const string MercadoPagoAccessToken = "MERCADO_PAGO__ACCESS_TOKEN";
    public const string MercadoPagoPublicKey = "MERCADO_PAGO__PUBLIC_KEY";
    public const string MercadoPagoWebhookSecret = "MERCADO_PAGO__WEBHOOK_SECRET";
    public const string MercadoPagoNotificationUrl = "MERCADO_PAGO__NOTIFICATION_URL";
    public const string MercadoPagoSuccessUrl = "MERCADO_PAGO__SUCCESS_URL";
    public const string MercadoPagoFailureUrl = "MERCADO_PAGO__FAILURE_URL";
    public const string MercadoPagoPendingUrl = "MERCADO_PAGO__PENDING_URL";
    public const string MercadoPagoApiBaseUrl = "MERCADO_PAGO__API_BASE_URL";

    // Pagamento — comportamento de polling/timeout
    public const string PagamentoPollingInterval = "PAGAMENTO__POLLING_INTERVAL";
    public const string PagamentoPollingMaxAttempts = "PAGAMENTO__POLLING_MAX_ATTEMPTS";
    public const string PagamentoOperationTimeout = "PAGAMENTO__OPERATION_TIMEOUT";
    public const string PagamentoLinkValidade = "PAGAMENTO__LINK_VALIDADE";

    // Cadastros API (M2M)
    public const string CadastrosApiBaseUrl = "CADASTROS_API__BASE_URL";

    // Outbox
    public const string OutboxPollInterval = "OUTBOX__POLL_INTERVAL";
    public const string OutboxBatchSize = "OUTBOX__BATCH_SIZE";
    public const string OutboxMaxRetries = "OUTBOX__MAX_RETRIES";
    public const string OutboxProcessedRetentionDays = "OUTBOX__PROCESSED_RETENTION_DAYS";
}
