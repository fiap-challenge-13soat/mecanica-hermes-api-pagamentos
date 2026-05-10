using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Outbox;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.Persistence.Outbox;

public class OutboxHealthMetricsTests
{
    private static (OutboxHealthMetrics metrics, MeterListener listener,
            ConcurrentBag<(string Instrument, long Value)> records)
        BuildWithListener()
    {
        var metrics = new OutboxHealthMetrics();
        var records = new ConcurrentBag<(string, long)>();

        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == OutboxHealthMetrics.MeterName)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, value, _, _) => records.Add((instrument.Name, value)));
        listener.Start();

        return (metrics, listener, records);
    }

    [Fact]
    public void MeterName_IsExpectedConstant()
    {
        OutboxHealthMetrics.MeterName.Should().Be("MecanicaHermes.Pagamento.Outbox");
    }

    [Fact]
    public void MessageDispatched_IncrementsDispatchedCounter()
    {
        var (metrics, listener, records) = BuildWithListener();
        using var _l = listener;
        using var _m = metrics;

        metrics.MessageDispatched();

        records.Should().ContainSingle(r => r.Instrument == "outbox.messages.dispatched" && r.Value == 1);
    }

    [Fact]
    public void MessageFailed_IncrementsFailedCounter()
    {
        var (metrics, listener, records) = BuildWithListener();
        using var _l = listener;
        using var _m = metrics;

        metrics.MessageFailed();

        records.Should().ContainSingle(r => r.Instrument == "outbox.messages.failed" && r.Value == 1);
    }

    [Fact]
    public void MessageDeadLettered_IncrementsDeadLetteredCounter()
    {
        var (metrics, listener, records) = BuildWithListener();
        using var _l = listener;
        using var _m = metrics;

        metrics.MessageDeadLettered();

        records.Should().ContainSingle(r => r.Instrument == "outbox.messages.dead_lettered" && r.Value == 1);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var metrics = new OutboxHealthMetrics();

        Action act = () => metrics.Dispose();

        act.Should().NotThrow();
    }
}
