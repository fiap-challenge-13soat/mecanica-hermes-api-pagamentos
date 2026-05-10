using Xunit;

namespace Mecanica.Hermes.Pagamento.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestWebApplicationFactory>
{
    public const string Name = "Integration Tests";
}
