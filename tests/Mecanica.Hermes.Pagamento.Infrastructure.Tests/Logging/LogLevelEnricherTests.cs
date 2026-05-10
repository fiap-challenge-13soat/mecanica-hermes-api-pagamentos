using FluentAssertions;
using Mecanica.Hermes.Pagamento.Infrastructure.Logging;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.Logging;

public class LogLevelEnricherTests
{
    private sealed class CapturingPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }

    private static LogEvent BuildLogEvent(LogEventLevel level)
    {
        var template = new MessageTemplateParser().Parse("hello");
        return new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            exception: null,
            template,
            properties: []);
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose, 0)]
    [InlineData(LogEventLevel.Debug, 1)]
    [InlineData(LogEventLevel.Information, 2)]
    [InlineData(LogEventLevel.Warning, 3)]
    [InlineData(LogEventLevel.Error, 4)]
    [InlineData(LogEventLevel.Fatal, 5)]
    public void Enrich_AddsLogLevelNumberPropertyMatchingSeverity(LogEventLevel level, int expectedNumber)
    {
        var enricher = new LogLevelEnricher();
        var logEvent = BuildLogEvent(level);
        var factory = new CapturingPropertyFactory();

        enricher.Enrich(logEvent, factory);

        logEvent.Properties.Should().ContainKey("LogLevelNumber");
        var prop = logEvent.Properties["LogLevelNumber"].Should().BeOfType<ScalarValue>().Subject;
        prop.Value.Should().Be(expectedNumber);
    }

    [Fact]
    public void Enrich_DoesNotOverwriteExistingProperty()
    {
        var enricher = new LogLevelEnricher();
        var logEvent = BuildLogEvent(LogEventLevel.Information);
        logEvent.AddPropertyIfAbsent(new LogEventProperty("LogLevelNumber", new ScalarValue(99)));
        var factory = new CapturingPropertyFactory();

        enricher.Enrich(logEvent, factory);

        var prop = logEvent.Properties["LogLevelNumber"].Should().BeOfType<ScalarValue>().Subject;
        prop.Value.Should().Be(99);
    }
}
