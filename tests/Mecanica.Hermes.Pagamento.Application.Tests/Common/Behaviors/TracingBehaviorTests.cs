using FluentAssertions;
using Mecanica.Hermes.Pagamento.Application.Common.Behaviors;
using Mecanica.Hermes.Shared.Application.Behaviors;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Commands;
using Mecanica.Hermes.Shared.Core.Results;
using MediatR;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Application.Tests.Common.Behaviors;

public class TracingBehaviorTests
{
    public sealed record DummyRequest : IRequest<Result>;

    public sealed record DummyPagamentoCommand(Guid PagamentoId) : IRequest<Result>, IPagamentoCommand;

    [Fact]
    public async Task Handle_ChamaNextEDevolveResposta()
    {
        var behavior = new TracingBehavior<DummyRequest, Result>();

        var nextCalled = false;
        Task<Result> Next(CancellationToken _)
        {
            nextCalled = true;
            return Task.FromResult(Result.Ok());
        }

        var response = await behavior.Handle(new DummyRequest(), Next, CancellationToken.None);

        nextCalled.Should().BeTrue();
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_RequestFailure_DevolveResultadoOriginal()
    {
        var behavior = new TracingBehavior<DummyRequest, Result>();
        Task<Result> Next(CancellationToken _) => Task.FromResult(Result.BadRequest("oops"));

        var response = await behavior.Handle(new DummyRequest(), Next, CancellationToken.None);

        response.IsFailure.Should().BeTrue();
        response.Errors.Should().Contain("oops");
    }

    [Fact]
    public async Task Handle_RequestComPagamentoCommand_NaoFalha()
    {
        var behavior = new TracingBehavior<DummyPagamentoCommand, Result>();
        Task<Result> Next(CancellationToken _) => Task.FromResult(Result.Ok());

        var cmd = new DummyPagamentoCommand(Guid.NewGuid());
        var response = await behavior.Handle(cmd, Next, CancellationToken.None);

        response.IsSuccess.Should().BeTrue();
    }
}
