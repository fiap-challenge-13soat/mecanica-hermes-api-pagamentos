using FluentAssertions;
using Mecanica.Hermes.Pagamento.Api.Presenter;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Api.Tests.Presenter;

public class ApiProblemDetailsTests
{
    [Fact]
    public void Constructor_ShouldExposeRfc7807Shape()
    {
        var problem = new ApiProblemDetails
        {
            Type = "https://example.com/probs/test",
            Title = "Algum titulo",
            Status = 400,
            Detail = "Algum detalhe",
            Instance = "/instance/123",
            Errors = ["err-1", "err-2"]
        };

        problem.Should().BeAssignableTo<ProblemDetails>();
        problem.Type.Should().Be("https://example.com/probs/test");
        problem.Title.Should().Be("Algum titulo");
        problem.Status.Should().Be(400);
        problem.Detail.Should().Be("Algum detalhe");
        problem.Instance.Should().Be("/instance/123");
        problem.Errors.Should().BeEquivalentTo("err-1", "err-2");
    }

    [Fact]
    public void Errors_ShouldDefaultToEmpty()
    {
        var problem = new ApiProblemDetails();

        problem.Errors.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Extensions_ShouldSupportTraceIdLikeAdditionalData()
    {
        var problem = new ApiProblemDetails { Status = 500 };
        problem.Extensions["traceId"] = "00-abc-def-01";

        problem.Extensions.Should().ContainKey("traceId");
        problem.Extensions["traceId"].Should().Be("00-abc-def-01");
    }
}
