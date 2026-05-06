namespace Mecanica.Hermes.Pagamento.Api;

/// <summary>
///     Tags utilizadas pelos health checks ASP.NET. Filtros como
///     <c>Predicate = hc =&gt; hc.Tags.Contains("ready")</c> dependem desses valores.
/// </summary>
internal static class HealthCheckTags
{
    public const string Ready = "ready";
    public const string Database = "database";
    public const string Messaging = "messaging";
    public const string Saga = "saga";
}
