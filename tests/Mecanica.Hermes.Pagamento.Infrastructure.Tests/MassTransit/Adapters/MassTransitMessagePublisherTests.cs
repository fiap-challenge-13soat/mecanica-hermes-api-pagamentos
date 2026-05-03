using FluentAssertions;
using MassTransit;
using Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Adapters;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.MassTransit.Adapters;

public class MassTransitMessagePublisherTests
{
    public sealed record TestMessage(string Value);

    [Fact]
    public async Task Publish_ForwardsToPublishEndpoint()
    {
        var endpoint = new Mock<IPublishEndpoint>();
        endpoint
            .Setup(e => e.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var publisher = new MassTransitMessagePublisher(
            endpoint.Object,
            NullLogger<MassTransitMessagePublisher>.Instance);

        var message = new TestMessage("hi");
        await publisher.Publish(message, CancellationToken.None);

        endpoint.Verify(
            e => e.Publish(It.Is<object>(o => ReferenceEquals(o, message)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Publish_TransientFailureThenSuccess_RetriesAndSucceeds()
    {
        var endpoint = new Mock<IPublishEndpoint>();
        var attempts = 0;
        endpoint
            .Setup(e => e.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns<object, CancellationToken>((_, _) =>
            {
                attempts++;
                if (attempts < 2) throw new InvalidOperationException("transient");
                return Task.CompletedTask;
            });

        var publisher = new MassTransitMessagePublisher(
            endpoint.Object,
            NullLogger<MassTransitMessagePublisher>.Instance);

        await publisher.Publish(new TestMessage("retry"), CancellationToken.None);

        attempts.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Publish_AlwaysFails_ThrowsBrokerUnavailableException()
    {
        var endpoint = new Mock<IPublishEndpoint>();
        endpoint
            .Setup(e => e.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));

        var publisher = new MassTransitMessagePublisher(
            endpoint.Object,
            NullLogger<MassTransitMessagePublisher>.Instance);

        Func<Task> act = () => publisher.Publish(new TestMessage("nope"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<BrokerUnavailableException>();
        ex.Which.InnerException.Should().BeOfType<InvalidOperationException>();
        ex.Which.Message.Should().Contain(nameof(TestMessage));
    }

    [Fact]
    public async Task Publish_OperationCanceled_DoesNotWrapInBrokerUnavailable()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var endpoint = new Mock<IPublishEndpoint>();
        endpoint
            .Setup(e => e.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns<object, CancellationToken>((_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        var publisher = new MassTransitMessagePublisher(
            endpoint.Object,
            NullLogger<MassTransitMessagePublisher>.Instance);

        Func<Task> act = () => publisher.Publish(new TestMessage("cancel"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
