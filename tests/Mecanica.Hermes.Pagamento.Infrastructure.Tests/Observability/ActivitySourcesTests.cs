using FluentAssertions;
using Mecanica.Hermes.Pagamento.Infrastructure.Observability;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.Observability;

public class ActivitySourcesTests
{
    [Fact]
    public void ApiName_HasExpectedValue()
    {
        ActivitySources.ApiName.Should().Be("MecanicaHermes.Pagamento.Api");
    }

    [Fact]
    public void Api_ActivitySourceIsConfiguredWithApiName()
    {
        ActivitySources.Api.Should().NotBeNull();
        ActivitySources.Api.Name.Should().Be(ActivitySources.ApiName);
        ActivitySources.Api.Version.Should().Be("1.0");
    }
}
