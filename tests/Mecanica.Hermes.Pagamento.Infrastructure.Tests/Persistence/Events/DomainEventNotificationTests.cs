using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Events;
using Mecanica.Hermes.Shared.Core.Domain;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.Persistence.Events;

public class DomainEventNotificationTests
{
    private sealed record SampleEvent(string Payload) : IDomainEvent;

    [Fact]
    public void Notification_WrapsEvent()
    {
        var evt = new SampleEvent("hello");

        var notification = new DomainEventNotification<SampleEvent>(evt);

        notification.Event.Should().BeSameAs(evt);
    }

    [Fact]
    public void Notification_RecordEqualityHoldsForSameEvent()
    {
        var evt = new SampleEvent("data");

        var a = new DomainEventNotification<SampleEvent>(evt);
        var b = new DomainEventNotification<SampleEvent>(evt);

        a.Should().Be(b);
    }
}
