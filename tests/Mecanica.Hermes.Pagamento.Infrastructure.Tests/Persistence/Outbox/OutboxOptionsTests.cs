using FluentAssertions;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Outbox;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.Persistence.Outbox;

public class OutboxOptionsTests
{
    [Fact]
    public void Defaults_AreExpectedValues()
    {
        var opts = new OutboxOptions();

        opts.PollInterval.Should().Be(TimeSpan.FromSeconds(2));
        opts.BatchSize.Should().Be(50);
        opts.MaxRetries.Should().Be(10);
        opts.ProcessedRetention.Should().Be(TimeSpan.FromDays(7));
        opts.ClaimTtl.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Setters_PersistValues()
    {
        var opts = new OutboxOptions
        {
            PollInterval = TimeSpan.FromSeconds(10),
            BatchSize = 200,
            MaxRetries = 4,
            ProcessedRetention = TimeSpan.FromDays(30),
            ClaimTtl = TimeSpan.FromMinutes(12)
        };

        opts.PollInterval.Should().Be(TimeSpan.FromSeconds(10));
        opts.BatchSize.Should().Be(200);
        opts.MaxRetries.Should().Be(4);
        opts.ProcessedRetention.Should().Be(TimeSpan.FromDays(30));
        opts.ClaimTtl.Should().Be(TimeSpan.FromMinutes(12));
    }
}
