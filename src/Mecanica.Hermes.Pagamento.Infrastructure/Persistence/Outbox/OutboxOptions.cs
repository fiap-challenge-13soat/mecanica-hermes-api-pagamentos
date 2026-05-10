namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Outbox;

internal sealed class OutboxOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public int BatchSize { get; set; } = 50;
    public int MaxRetries { get; set; } = 10;
    public TimeSpan ProcessedRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    ///     Tempo após o qual um documento "Processing" pode ser reivindicado por outra instância
    ///     (defesa contra travamentos de processador). Default: 5 minutos.
    /// </summary>
    public TimeSpan ClaimTtl { get; set; } = TimeSpan.FromMinutes(5);
}
