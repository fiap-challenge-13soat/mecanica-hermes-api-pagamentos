using FluentAssertions;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Api.Tests;

public class HealthCheckTagsTests
{
    [Fact]
    public void Ready_ShouldExposeExpectedTagValue()
    {
        // The constant is internal — this test runs in the same assembly via InternalsVisibleTo or
        // direct reflection because the type is internal. Use reflection to keep coupling loose.
        var type = typeof(IApiAssemblyMarker).Assembly
            .GetType("Mecanica.Hermes.Pagamento.Api.HealthCheckTags", throwOnError: true)!;

        var ready = (string)type.GetField("Ready")!.GetValue(null)!;
        var database = (string)type.GetField("Database")!.GetValue(null)!;
        var messaging = (string)type.GetField("Messaging")!.GetValue(null)!;

        ready.Should().Be("ready");
        database.Should().Be("database");
        messaging.Should().Be("messaging");
    }
}
