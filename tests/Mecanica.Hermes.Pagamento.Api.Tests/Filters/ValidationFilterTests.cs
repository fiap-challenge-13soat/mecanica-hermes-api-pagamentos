using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Mecanica.Hermes.Pagamento.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Api.Tests.Filters;

public class ValidationFilterTests
{
    public sealed record TestRequest(string Name);

    private static TestEndpointFilterInvocationContext BuildContext(params object?[] args)
    {
        var http = new DefaultHttpContext();
        return new TestEndpointFilterInvocationContext(http, args);
    }

    private sealed class TestEndpointFilterInvocationContext(HttpContext http, IList<object?> args)
        : EndpointFilterInvocationContext
    {
        public override HttpContext HttpContext { get; } = http;
        public override IList<object?> Arguments { get; } = args;

        public override T GetArgument<T>(int index)
        {
            return (T)Arguments[index]!;
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenArgumentMissing_CallsNext()
    {
        var validator = new Mock<IValidator<TestRequest>>(MockBehavior.Strict);
        var filter = new ValidationFilter<TestRequest>(validator.Object);

        var context = BuildContext("not-a-request");
        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        var result = await filter.InvokeAsync(context, next);

        nextCalled.Should().BeTrue();
        result.Should().BeAssignableTo<IResult>();
        validator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationFails_ReturnsProblemWithErrors()
    {
        var validator = new Mock<IValidator<TestRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Name", "obrigatorio"),
                new ValidationFailure("Name", "muito curto")
            }));

        var filter = new ValidationFilter<TestRequest>(validator.Object);
        var request = new TestRequest("");
        var context = BuildContext(request);

        EndpointFilterDelegate next = _ =>
        {
            throw new InvalidOperationException("next deveria nao ser chamado");
        };

        var result = await filter.InvokeAsync(context, next);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Title.Should().Be("Requisição Inválida.");
        problem.ProblemDetails.Extensions.Should().ContainKey("errors");

        var errors = problem.ProblemDetails.Extensions["errors"]!;
        errors.Should().BeAssignableTo<IEnumerable<string>>();
        ((IEnumerable<string>)errors).Should().BeEquivalentTo("obrigatorio", "muito curto");
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationPasses_CallsNext()
    {
        var validator = new Mock<IValidator<TestRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<TestRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var filter = new ValidationFilter<TestRequest>(validator.Object);
        var request = new TestRequest("Joao");
        var context = BuildContext(request);

        var nextCalled = false;
        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        var result = await filter.InvokeAsync(context, next);

        nextCalled.Should().BeTrue();
        result.Should().BeAssignableTo<IResult>();
    }
}
