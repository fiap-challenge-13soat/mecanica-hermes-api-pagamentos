using System.Diagnostics.Metrics;
using FluentAssertions;
using MassTransit;
using Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.MassTransit.Observability;

[Trait("Category", "Unit")]
public sealed class MessagingFaultObserverTests : IDisposable
{
    private readonly List<CapturedMeasurement> _capturedMeasurements = [];
    private readonly MeterListener _listener;
    private readonly DlqMetrics _metrics;
    private readonly MessagingFaultObserver _sut;

    public MessagingFaultObserverTests()
    {
        _metrics = new DlqMetrics();
        _sut = new MessagingFaultObserver(_metrics, NullLogger<MessagingFaultObserver>.Instance);

        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == DlqMetrics.MeterName)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            _capturedMeasurements.Add(new CapturedMeasurement(
                instrument.Name,
                value,
                tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));
        });
        _listener.Start();
    }

    public void Dispose()
    {
        _listener.Dispose();
        _metrics.Dispose();
    }

    [Fact]
    public async Task ConsumeFault_IncrementaContadorComTags()
    {
        var context = new Mock<ConsumeContext<TestMessage>>();
        context.SetupGet(c => c.MessageId).Returns(Guid.NewGuid());

        await _sut.ConsumeFault(context.Object, new InvalidOperationException("boom"));

        _capturedMeasurements.Should().ContainSingle();
        var measurement = _capturedMeasurements[0];
        measurement.InstrumentName.Should().Be("messaging.consume_faults");
        measurement.Value.Should().Be(1);
        measurement.Tags["message_type"].Should().Be(nameof(TestMessage));
        measurement.Tags["exception_type"].Should().Be(nameof(InvalidOperationException));
    }

    [Fact]
    public async Task PreConsume_NaoEmiteMetrica()
    {
        var context = new Mock<ConsumeContext<TestMessage>>();
        context.SetupGet(c => c.MessageId).Returns(Guid.NewGuid());

        await _sut.PreConsume(context.Object);

        _capturedMeasurements.Should().BeEmpty(
            "PreConsume é noop — só ConsumeFault deve emitir métrica de DLQ");
    }

    [Fact]
    public async Task PostConsume_NaoEmiteMetrica()
    {
        var context = new Mock<ConsumeContext<TestMessage>>();
        context.SetupGet(c => c.MessageId).Returns(Guid.NewGuid());

        await _sut.PostConsume(context.Object);

        _capturedMeasurements.Should().BeEmpty(
            "PostConsume (sucesso) é noop — só ConsumeFault deve emitir métrica de DLQ");
    }

    [Fact]
    public async Task ConsumeFault_MultiplosTipos_GeraTagsDistintas()
    {
        var ctxA = new Mock<ConsumeContext<TestMessage>>();
        ctxA.SetupGet(c => c.MessageId).Returns(Guid.NewGuid());

        var ctxB = new Mock<ConsumeContext<OutroMessage>>();
        ctxB.SetupGet(c => c.MessageId).Returns(Guid.NewGuid());

        await _sut.ConsumeFault(ctxA.Object, new TimeoutException("a"));
        await _sut.ConsumeFault(ctxB.Object, new HttpRequestException("b"));

        _capturedMeasurements.Should().HaveCount(2);
        _capturedMeasurements.Select(m => m.Tags["message_type"]).Should()
            .BeEquivalentTo([nameof(TestMessage), nameof(OutroMessage)]);
        _capturedMeasurements.Select(m => m.Tags["exception_type"]).Should()
            .BeEquivalentTo([nameof(TimeoutException), nameof(HttpRequestException)]);
    }

    private sealed record CapturedMeasurement(
        string InstrumentName,
        long Value,
        Dictionary<string, object?> Tags);

    public sealed record TestMessage(Guid Id);

    public sealed record OutroMessage(string Payload);
}
