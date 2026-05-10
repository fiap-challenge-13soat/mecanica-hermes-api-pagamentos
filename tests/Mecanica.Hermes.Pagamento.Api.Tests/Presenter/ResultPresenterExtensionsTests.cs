using System.Net;
using FluentAssertions;
using Mecanica.Hermes.Pagamento.Api.Presenter;
using Mecanica.Hermes.Shared.AspNetCore.Presenter;
using Mecanica.Hermes.Shared.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Api.Tests.Presenter;

public class ResultPresenterExtensionsTests
{
    private sealed record AppModel(string Value);
    private sealed record ApiModel(string Value);

    // ------------ Result<T> with map (success path) ------------

    [Fact]
    public void Present_WithSuccessfulResult_ReturnsOkResultWithMappedBody()
    {
        var result = Result<AppModel>.Ok(new AppModel("hello"));

        var output = result.Present(app => new ApiModel(app.Value.ToUpperInvariant()));

        var ok = output.Should().BeOfType<Ok<ApiModel>>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().BeEquivalentTo(new ApiModel("HELLO"));
    }

    [Fact]
    public void Present_WithSuccessfulResultAndCustomSuccessFactory_ReturnsCustomResult()
    {
        var result = Result<AppModel>.Created(new AppModel("created"));

        var output = result.Present(
            app => new ApiModel(app.Value),
            api => Results.Created($"/api/items/1", api));

        var created = output.Should().BeOfType<Created<ApiModel>>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Value.Should().BeEquivalentTo(new ApiModel("created"));
        created.Location.Should().Be("/api/items/1");
    }

    // ------------ Result<T> failure paths ------------

    [Fact]
    public void Present_WithNotFound_ReturnsNotFoundResult()
    {
        var result = Result<AppModel>.NotFound();

        var output = result.Present(app => new ApiModel(app.Value));

        output.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void Present_WithBadRequest_ReturnsBadRequestProblemDetails()
    {
        var result = Result<AppModel>.BadRequest(["campo X invalido", "campo Y obrigatorio"]);

        var output = result.Present(app => new ApiModel(app.Value));

        var bad = output.Should().BeOfType<BadRequest<ApiProblemDetails>>().Subject;
        bad.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        bad.Value.Should().NotBeNull();
        bad.Value!.Status.Should().Be(400);
        bad.Value.Title.Should().Be("Requisição Inválida.");
        bad.Value.Errors.Should().BeEquivalentTo("campo X invalido", "campo Y obrigatorio");
    }

    [Fact]
    public void Present_WithConflict_ReturnsConflictProblemDetails()
    {
        var result = Result<AppModel>.Conflict("ja existe");

        var output = result.Present(app => new ApiModel(app.Value));

        var conflict = output.Should().BeOfType<Conflict<ApiProblemDetails>>().Subject;
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        conflict.Value!.Status.Should().Be(409);
        conflict.Value.Errors.Should().Contain("ja existe");
    }

    [Fact]
    public void Present_WithUnauthorized_ReturnsUnauthorizedResult()
    {
        var result = Result<AppModel>.Unauthorized();

        var output = result.Present(app => new ApiModel(app.Value));

        output.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void Present_WithInternalServerError_ReturnsProblemResult()
    {
        var result = Result<AppModel>.InternalServerError("falha critica");

        var output = result.Present(app => new ApiModel(app.Value));

        var problem = output.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        problem.ProblemDetails.Should().BeOfType<ApiProblemDetails>();
        var apiProblem = (ApiProblemDetails)problem.ProblemDetails;
        apiProblem.Errors.Should().Contain("falha critica");
    }

    [Fact]
    public void Present_WithNoContentFailure_ReturnsNoContentResult()
    {
        // NoContent (204) is technically success-like; ensure the failure-shaped switch handles it
        // by constructing a Result with that status and at least one error so IsFailure is true.
        var result = new Result<AppModel>(HttpStatusCode.NoContent);
        result.Errors.Add("forcar falha");

        var output = result.Present(app => new ApiModel(app.Value));

        output.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    // ------------ Result<T> without map (parameterless overload) ------------

    [Fact]
    public void Present_WithoutMap_OnSuccess_ReturnsNoContent()
    {
        var result = Result<AppModel>.Ok(new AppModel("hi"));

        var output = result.Present();

        output.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public void Present_WithoutMap_OnNotFound_ReturnsNotFound()
    {
        var result = Result<AppModel>.NotFound();

        var output = result.Present();

        output.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void Present_WithoutMap_OnBadRequest_ReturnsBadRequestProblem()
    {
        var result = Result<AppModel>.BadRequest("x");

        var output = result.Present();

        output.Should().BeOfType<BadRequest<ApiProblemDetails>>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void Present_WithoutMap_OnConflict_ReturnsConflictProblem()
    {
        var result = Result<AppModel>.Conflict("dup");

        var output = result.Present();

        output.Should().BeOfType<Conflict<ApiProblemDetails>>()
            .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void Present_WithoutMap_OnInternalServerError_ReturnsProblem()
    {
        var result = Result<AppModel>.InternalServerError("boom");

        var output = result.Present();

        output.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    // ------------ Non-generic Result ------------

    [Fact]
    public void Present_NonGenericResult_OnAcceptedSuccess_ReturnsAccepted()
    {
        var result = Result.Accepted();

        var output = result.Present();

        output.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status202Accepted);
    }

    [Fact]
    public void Present_NonGenericResult_OnOkSuccess_ReturnsNoContent()
    {
        var result = Result.Ok();

        var output = result.Present();

        output.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public void Present_NonGenericResult_OnBadRequest_ReturnsBadRequestProblem()
    {
        var result = Result.BadRequest("invalido");

        var output = result.Present();

        var bad = output.Should().BeOfType<BadRequest<ApiProblemDetails>>().Subject;
        bad.Value!.Errors.Should().Contain("invalido");
    }

    [Fact]
    public void Present_NonGenericResult_OnNotFound_ReturnsNotFound()
    {
        var result = Result.NotFound();

        var output = result.Present();

        output.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void Present_NonGenericResult_OnConflictWithErrors_ReturnsConflictProblem()
    {
        var result = new Result(HttpStatusCode.Conflict);
        result.Errors.Add("conflito X");

        var output = result.Present();

        var conflict = output.Should().BeOfType<Conflict<ApiProblemDetails>>().Subject;
        conflict.Value!.Errors.Should().Contain("conflito X");
    }

    [Fact]
    public void Present_NonGenericResult_OnUnauthorized_ReturnsUnauthorized()
    {
        var result = new Result(HttpStatusCode.Unauthorized);
        result.Errors.Add("nao autorizado");

        var output = result.Present();

        output.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void Present_NonGenericResult_OnInternalServerError_ReturnsProblem()
    {
        var result = new Result(HttpStatusCode.InternalServerError);
        result.Errors.Add("falha");

        var output = result.Present();

        output.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void Present_NonGenericResult_OnNoContentFailure_ReturnsNoContent()
    {
        var result = new Result(HttpStatusCode.NoContent);
        result.Errors.Add("forca falha");

        var output = result.Present();

        output.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }
}
