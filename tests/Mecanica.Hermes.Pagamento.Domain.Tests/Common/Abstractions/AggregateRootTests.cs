using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Shared.Core.Domain;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Common.Abstractions;

public class AggregateRootTests
{
    private sealed record FakeEvent(string Name) : IDomainEvent;

    private sealed class FakeAggregate : AggregateRoot
    {
        public FakeAggregate() { }
        public FakeAggregate(Guid id) : base(id) { }
        public void Raise(IDomainEvent e) => AddDomainEvent(e);
    }

    [Fact]
    public void DomainEvents_DefaultEhVazio()
    {
        var aggregate = new FakeAggregate();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddDomainEvent_AdicionaAoList()
    {
        var aggregate = new FakeAggregate();
        var evt = new FakeEvent("e1");

        aggregate.Raise(evt);

        aggregate.DomainEvents.Should().ContainSingle().Which.Should().Be(evt);
    }

    [Fact]
    public void AddDomainEvent_PreservaOrdem()
    {
        var aggregate = new FakeAggregate();
        var e1 = new FakeEvent("e1");
        var e2 = new FakeEvent("e2");
        var e3 = new FakeEvent("e3");

        aggregate.Raise(e1);
        aggregate.Raise(e2);
        aggregate.Raise(e3);

        aggregate.DomainEvents.Should().ContainInOrder(e1, e2, e3);
    }

    [Fact]
    public void ClearDomainEvents_RemoveTodosOsEventos()
    {
        var aggregate = new FakeAggregate();
        aggregate.Raise(new FakeEvent("e1"));
        aggregate.Raise(new FakeEvent("e2"));

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ConstrutorComId_DefiniId()
    {
        var id = Guid.NewGuid();

        var aggregate = new FakeAggregate(id);

        aggregate.Id.Should().Be(id);
    }
}
