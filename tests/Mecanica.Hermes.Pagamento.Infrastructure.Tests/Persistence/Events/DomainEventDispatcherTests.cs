using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Events;
using Mecanica.Hermes.Shared.Core.Domain;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.Persistence.Events;

public class DomainEventDispatcherTests
{
    private sealed record AlphaEvent(int Value) : IDomainEvent;
    private sealed record BetaEvent(string Name) : IDomainEvent;

    private static bool IsAlphaWrapping(INotification n, AlphaEvent expected)
    {
        return n is DomainEventNotification<AlphaEvent> dn && dn.Event.Equals(expected);
    }

    private static bool IsBetaWrapping(INotification n, BetaEvent expected)
    {
        return n is DomainEventNotification<BetaEvent> dn && dn.Event.Equals(expected);
    }

    [Fact]
    public async Task DispatchAsync_NoEvents_DoesNotPublish()
    {
        var publisher = new Mock<IPublisher>();
        var dispatcher = new DomainEventDispatcher(publisher.Object, NullLogger<DomainEventDispatcher>.Instance);

        await dispatcher.DispatchAsync([], CancellationToken.None);

        publisher.Verify(
            p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_SingleEvent_PublishesWrappedNotification()
    {
        var publisher = new Mock<IPublisher>();
        var dispatcher = new DomainEventDispatcher(publisher.Object, NullLogger<DomainEventDispatcher>.Instance);
        var evt = new AlphaEvent(42);

        await dispatcher.DispatchAsync([evt], CancellationToken.None);

        publisher.Verify(
            p => p.Publish(
                It.Is<INotification>(n => IsAlphaWrapping(n, evt)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_MultipleEvents_AllArePublishedInOrder()
    {
        var publisher = new Mock<IPublisher>();
        var captured = new List<INotification>();
        publisher
            .Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Callback<INotification, CancellationToken>((n, _) => captured.Add(n))
            .Returns(Task.CompletedTask);

        var dispatcher = new DomainEventDispatcher(publisher.Object, NullLogger<DomainEventDispatcher>.Instance);

        var alpha = new AlphaEvent(1);
        var beta = new BetaEvent("two");

        await dispatcher.DispatchAsync([alpha, beta], CancellationToken.None);

        captured.Should().HaveCount(2);
        captured[0].Should().BeOfType<DomainEventNotification<AlphaEvent>>()
            .Which.Event.Should().Be(alpha);
        captured[1].Should().BeOfType<DomainEventNotification<BetaEvent>>()
            .Which.Event.Should().Be(beta);
    }

    [Fact]
    public async Task DispatchAsync_HandlerThrows_ContinuesIterating()
    {
        var publisher = new Mock<IPublisher>();
        var alpha = new AlphaEvent(1);
        var beta = new BetaEvent("two");
        publisher
            .Setup(p => p.Publish(
                It.Is<INotification>(n => IsAlphaWrapping(n, alpha)),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var dispatcher = new DomainEventDispatcher(publisher.Object, NullLogger<DomainEventDispatcher>.Instance);

        await dispatcher.DispatchAsync([alpha, beta], CancellationToken.None);

        publisher.Verify(
            p => p.Publish(
                It.Is<INotification>(n => IsBetaWrapping(n, beta)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
