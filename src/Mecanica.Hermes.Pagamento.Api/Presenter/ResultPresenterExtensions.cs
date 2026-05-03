using System.Net;
using Mecanica.Hermes.Pagamento.Domain.Common.Results;
using Microsoft.AspNetCore.Http;

namespace Mecanica.Hermes.Pagamento.Api.Presenter;

public static class ResultPresenterExtensions
{
    public static IResult Present<TApp, TApi>(
        this Result<TApp> result,
        Func<TApp, TApi> map,
        Func<TApi, IResult>? success = null)
    {
        if (result.IsFailure)
            return result.StatusCode switch
            {
                HttpStatusCode.NoContent => Results.NoContent(),
                HttpStatusCode.NotFound => Results.NotFound(),
                HttpStatusCode.Conflict => result.AsConflictResult(),
                HttpStatusCode.Unauthorized => Results.Unauthorized(),
                HttpStatusCode.BadRequest => result.AsBadRequestResult(),
                _ => result.AsInternalServerErrorResult()
            };

        var apiModel = map(result.Data!);

        if (success != null)
            return success(apiModel);

        return Results.Ok(apiModel);
    }

    public static IResult Present<TApp>(this Result<TApp> result)
    {
        if (result.IsSuccess)
            return Results.NoContent();

        return result.StatusCode switch
        {
            HttpStatusCode.NoContent => Results.NoContent(),
            HttpStatusCode.NotFound => Results.NotFound(),
            HttpStatusCode.Conflict => result.AsConflictResult(),
            HttpStatusCode.Unauthorized => Results.Unauthorized(),
            HttpStatusCode.BadRequest => result.AsBadRequestResult(),
            _ => result.AsInternalServerErrorResult()
        };
    }

    public static IResult Present(this Result result)
    {
        if (result.IsSuccess)
            return result.StatusCode == HttpStatusCode.Accepted
                ? Results.Accepted()
                : Results.NoContent();

        return result.StatusCode switch
        {
            HttpStatusCode.NoContent => Results.NoContent(),
            HttpStatusCode.NotFound => Results.NotFound(),
            HttpStatusCode.Conflict => result.AsConflictResult(),
            HttpStatusCode.Unauthorized => Results.Unauthorized(),
            HttpStatusCode.BadRequest => result.AsBadRequestResult(),
            _ => result.AsInternalServerErrorResult()
        };
    }

    private static IResult AsConflictResult<TApp>(this Result<TApp> result)
    {
        return Results.Conflict(BuildProblemDetails(
            HttpStatusCode.Conflict,
            "Conflito.",
            "Registro já cadastrado na base de dados.",
            result.Errors));
    }

    private static IResult AsConflictResult(this Result result)
    {
        return Results.Conflict(BuildProblemDetails(
            HttpStatusCode.Conflict,
            "Conflito.",
            "Registro já cadastrado na base de dados.",
            result.Errors));
    }

    private static IResult AsBadRequestResult<TApp>(this Result<TApp> result)
    {
        return Results.BadRequest(BuildProblemDetails(
            HttpStatusCode.BadRequest,
            "Requisição Inválida.",
            "A requisição contém parâmetros inválidos.",
            result.Errors));
    }

    private static IResult AsBadRequestResult(this Result result)
    {
        return Results.BadRequest(BuildProblemDetails(
            HttpStatusCode.BadRequest,
            "Requisição Inválida.",
            "A requisição contém parâmetros inválidos.",
            result.Errors));
    }

    private static IResult AsInternalServerErrorResult<TApp>(this Result<TApp> result)
    {
        return Results.Problem(BuildProblemDetails(
            HttpStatusCode.InternalServerError,
            "Erro Interno.",
            "Ocorreu um erro inesperado no servidor.",
            result.Errors));
    }

    private static IResult AsInternalServerErrorResult(this Result result)
    {
        return Results.Problem(BuildProblemDetails(
            HttpStatusCode.InternalServerError,
            "Erro Interno.",
            "Ocorreu um erro inesperado no servidor.",
            result.Errors));
    }

    private static ApiProblemDetails BuildProblemDetails(
        HttpStatusCode status,
        string title,
        string detail,
        List<string> errors)
    {
        return new ApiProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail,
            Errors = errors
        };
    }
}
