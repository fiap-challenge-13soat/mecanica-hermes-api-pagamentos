using FluentAssertions;
using Mecanica.Hermes.Pagamento.Infrastructure.MercadoPago;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.MercadoPago;

public class MercadoPagoOptionsTests
{
    [Fact]
    public void Defaults_AllStringPropertiesEmpty()
    {
        var opts = new MercadoPagoOptions();

        opts.AccessToken.Should().BeEmpty();
        opts.PublicKey.Should().BeEmpty();
        opts.WebhookSecret.Should().BeEmpty();
        opts.NotificationUrl.Should().BeEmpty();
        opts.SuccessUrl.Should().BeEmpty();
        opts.FailureUrl.Should().BeEmpty();
        opts.PendingUrl.Should().BeEmpty();
    }

    [Fact]
    public void Setters_PersistValues()
    {
        var opts = new MercadoPagoOptions
        {
            AccessToken = "tok",
            PublicKey = "pub",
            WebhookSecret = "secret",
            NotificationUrl = "https://example/notify",
            SuccessUrl = "https://example/ok",
            FailureUrl = "https://example/fail",
            PendingUrl = "https://example/pending"
        };

        opts.AccessToken.Should().Be("tok");
        opts.PublicKey.Should().Be("pub");
        opts.WebhookSecret.Should().Be("secret");
        opts.NotificationUrl.Should().Be("https://example/notify");
        opts.SuccessUrl.Should().Be("https://example/ok");
        opts.FailureUrl.Should().Be("https://example/fail");
        opts.PendingUrl.Should().Be("https://example/pending");
    }
}
