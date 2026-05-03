using FluentAssertions;
using Mecanica.Hermes.Pagamento.Application;
using Mecanica.Hermes.Pagamento.Domain;
using Mecanica.Hermes.Pagamento.Infrastructure;
using NetArchTest.Rules;
using Xunit;

namespace Mecanica.Hermes.Pagamento.ArchitectureTests;

public class ArchitectureTests
{
    private const string DomainNs = "Mecanica.Hermes.Pagamento.Domain";
    private const string ApplicationNs = "Mecanica.Hermes.Pagamento.Application";
    private const string InfrastructureNs = "Mecanica.Hermes.Pagamento.Infrastructure";
    private const string ApiNs = "Mecanica.Hermes.Pagamento.Api";

    [Fact]
    public void Domain_NaoDeveDependerDeOutrasCamadas()
    {
        var result = Types.InAssembly(typeof(IDomainAssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, ApiNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Domain deve permanecer puro — falha em: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_NaoDeveDependerDeInfrastructureOuApi()
    {
        var result = Types.InAssembly(typeof(IApplicationAssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, ApiNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Application não pode depender de Infrastructure/Api — falha em: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Consumers_DevemEstarApenasNaApplication()
    {
        var result = Types.InAssembly(typeof(IInfrastructureAssemblyMarker).Assembly)
            .That()
            .HaveNameEndingWith("Consumer")
            .Should()
            .ResideInNamespaceMatching(@"^Mecanica\.Hermes\.Pagamento\.Application\..+")
            .GetResult();

        // Result returns failing list quando há classes Consumer fora da Application.
        result.IsSuccessful.Should().BeTrue();
    }
}
