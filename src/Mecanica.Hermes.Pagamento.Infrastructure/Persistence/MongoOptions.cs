namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence;

/// <summary>
///     Opções de conexão com o MongoDB. Lidas das env vars <c>MONGODB__*</c> no startup.
/// </summary>
internal sealed class MongoOptions
{
    public const string DefaultDatabase = "mechermes_pagamento";
    public const string DefaultSagaDatabase = "mechermes_pagamento_saga";

    public const string PagamentosCollection = "pagamentos";
    public const string OutboxCollection = "pagamento_outbox";
    public const string WebhookEventsCollection = "webhook_events";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = DefaultDatabase;
    public string SagaDatabaseName { get; set; } = DefaultSagaDatabase;
}
