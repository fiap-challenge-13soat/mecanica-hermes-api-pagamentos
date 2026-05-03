using System.Diagnostics.Metrics;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Outbox;

internal sealed class OutboxHealthMetrics : IDisposable
{
    public const string MeterName = "MecanicaHermes.Pagamento.Outbox";
    private readonly Counter<long> _deadLettered;
    private readonly Counter<long> _dispatched;
    private readonly Counter<long> _failed;

    private readonly Meter _meter;

    public OutboxHealthMetrics()
    {
        _meter = new Meter(MeterName);
        _dispatched = _meter.CreateCounter<long>(
            "outbox.messages.dispatched",
            description: "Mensagens do Outbox despachadas com sucesso.");
        _failed = _meter.CreateCounter<long>(
            "outbox.messages.failed",
            description: "Falhas transientes ao despachar mensagens do Outbox (ainda em retry).");
        _deadLettered = _meter.CreateCounter<long>(
            "outbox.messages.dead_lettered",
            description: "Mensagens do Outbox movidas para Dead Letter após esgotar tentativas.");
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    public void MessageDispatched()
    {
        _dispatched.Add(1);
    }

    public void MessageFailed()
    {
        _failed.Add(1);
    }

    public void MessageDeadLettered()
    {
        _deadLettered.Add(1);
    }
}
