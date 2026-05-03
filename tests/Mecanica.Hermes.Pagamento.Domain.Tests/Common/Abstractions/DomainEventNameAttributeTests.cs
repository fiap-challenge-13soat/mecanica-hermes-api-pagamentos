using System.Reflection;
using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Events;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Common.Abstractions;

public class DomainEventNameAttributeTests
{
    [Fact]
    public void Construtor_GuardaNome()
    {
        var attribute = new DomainEventNameAttribute("pagamento.teste.v1");

        attribute.Name.Should().Be("pagamento.teste.v1");
    }

    [Fact]
    public void Aplicado_EmEvento_ExpoeNomeViaReflection()
    {
        var attribute = typeof(PagamentoCriadoEvent).GetCustomAttribute<DomainEventNameAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Name.Should().Be("pagamento.criado.v1");
    }

    [Fact]
    public void AttributeUsage_NaoPermiteHeranca()
    {
        var usage = typeof(DomainEventNameAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        usage.Should().NotBeNull();
        usage!.Inherited.Should().BeFalse();
        usage.ValidOn.Should().Be(AttributeTargets.Class);
    }
}
