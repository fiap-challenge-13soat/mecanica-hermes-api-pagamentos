using FluentAssertions;
using Mecanica.Hermes.Pagamento.Application.Common.Behaviors;
using Mecanica.Hermes.Shared.Application.Behaviors;
using Mecanica.Hermes.Shared.Core.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Common.Behaviors;

public class LoggingBehaviorTests
{
    public sealed record DummyRequest(string Name) : IRequest<Result>;

    [Fact]
    public async Task Handle_RequestSuccess_ChamaNextEDevolveResposta()
    {
        var behavior = new LoggingBehavior<DummyRequest, Result>(
            NullLogger<LoggingBehavior<DummyRequest, Result>>.Instance);

        var nextCalled = false;
        Task<Result> Next(CancellationToken _)
        {
            nextCalled = true;
            return Task.FromResult(Result.Ok());
        }

        var response = await behavior.Handle(new DummyRequest("x"), Next, CancellationToken.None);

        nextCalled.Should().BeTrue();
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_RequestFailure_ChamaNextEDevolveResposta()
    {
        var behavior = new LoggingBehavior<DummyRequest, Result>(
            NullLogger<LoggingBehavior<DummyRequest, Result>>.Instance);

        Task<Result> Next(CancellationToken _) => Task.FromResult(Result.BadRequest("boom"));

        var response = await behavior.Handle(new DummyRequest("y"), Next, CancellationToken.None);

        response.IsFailure.Should().BeTrue();
        response.Errors.Should().ContainSingle(e => e == "boom");
    }

    [Fact]
    public async Task Handle_LogaInformationESuccess()
    {
        var logger = new Mock<ILogger<LoggingBehavior<DummyRequest, Result>>>();
        var behavior = new LoggingBehavior<DummyRequest, Result>(logger.Object);

        Task<Result> Next(CancellationToken _) => Task.FromResult(Result.Ok());

        await behavior.Handle(new DummyRequest("z"), Next, CancellationToken.None);

        logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
