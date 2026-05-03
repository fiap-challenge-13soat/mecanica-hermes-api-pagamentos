using System.Net;

#pragma warning disable CA1000 // Static factory methods on generic Result<T> are intentional API design

namespace Mecanica.Hermes.Pagamento.Domain.Common.Results;

public class Result(HttpStatusCode statusCode)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public bool IsSuccess => Errors.Count == 0 && (int)StatusCode >= 200 && (int)StatusCode <= 202;
    public bool IsFailure => !IsSuccess;
    public List<string> Errors { get; } = [];

    public static Result Ok()
    {
        return new Result(HttpStatusCode.OK);
    }

    public static Result Accepted()
    {
        return new Result(HttpStatusCode.Accepted);
    }

    public static Result BadRequest(string errorMessage)
    {
        var result = new Result(HttpStatusCode.BadRequest);
        result.Errors.Add(errorMessage);
        return result;
    }

    public static Result BadRequest(IEnumerable<string> errorMessages)
    {
        var result = new Result(HttpStatusCode.BadRequest);
        foreach (var errorMessage in errorMessages) result.Errors.Add(errorMessage);

        return result;
    }

    public static Result NotFound()
    {
        return new Result(HttpStatusCode.NotFound);
    }
}

public class Result<TValue>(HttpStatusCode statusCode, TValue? data = default) : Result(statusCode)
{
    public TValue? Data { get; } = data;

    public new static Result<TValue> Ok()
    {
        return new Result<TValue>(HttpStatusCode.OK);
    }

    public static Result<TValue> Ok(TValue? value)
    {
        return new Result<TValue>(HttpStatusCode.OK, value);
    }

    public static Result<TValue> Forbidden()
    {
        return new Result<TValue>(HttpStatusCode.Forbidden);
    }

    public static Result<TValue> Unauthorized()
    {
        return new Result<TValue>(HttpStatusCode.Unauthorized);
    }

    public static Result<TValue> NoContent()
    {
        return new Result<TValue>(HttpStatusCode.NoContent);
    }

    public new static Result<TValue> NotFound()
    {
        return new Result<TValue>(HttpStatusCode.NotFound);
    }

    public static Result<TValue> Created(TValue value)
    {
        return new Result<TValue>(HttpStatusCode.Created, value);
    }

    public static Result<TValue> Conflict(string errorMessage)
    {
        var result = new Result<TValue>(HttpStatusCode.Conflict);
        result.Errors.Add(errorMessage);
        return result;
    }

    public new static Result<TValue> BadRequest(string errorMessage)
    {
        var result = new Result<TValue>(HttpStatusCode.BadRequest);
        result.Errors.Add(errorMessage);
        return result;
    }

    public new static Result<TValue> BadRequest(IEnumerable<string> errorMessages)
    {
        var result = new Result<TValue>(HttpStatusCode.BadRequest);
        foreach (var errorMessage in errorMessages) result.Errors.Add(errorMessage);

        return result;
    }

    public static Result<TValue> InternalServerError(string errorMessage)
    {
        var result = new Result<TValue>(HttpStatusCode.InternalServerError);
        result.Errors.Add(errorMessage);
        return result;
    }
}
