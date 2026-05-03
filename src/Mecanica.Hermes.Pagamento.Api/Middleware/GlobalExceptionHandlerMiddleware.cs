using System.Net;
using System.Text.Json;
using Mecanica.Hermes.Pagamento.Api.Presenter;
using Mecanica.Hermes.Pagamento.Domain.Common;
using Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Adapters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mecanica.Hermes.Pagamento.Api.Middleware;

public sealed class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlerMiddleware> logger,
    IHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocorreu uma exceção não tratada: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex, environment);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception,
        IHostEnvironment environment)
    {
        context.Response.ContentType = "application/problem+json";

        var isDevelopmentOrTesting =
            environment.IsDevelopment() || environment.EnvironmentName == EnvironmentNames.Testing;

        var problemDetails = exception switch
        {
            BrokerUnavailableException brokerEx => CreateProblemDetails(
                HttpStatusCode.ServiceUnavailable,
                "Serviço Indisponível",
                "O serviço de mensageria está temporariamente indisponível. Tente novamente em instantes.",
                [brokerEx.Message]),

            ArgumentException argEx => CreateProblemDetails(
                HttpStatusCode.BadRequest,
                "Argumento Inválido",
                isDevelopmentOrTesting
                    ? $"{argEx.Message}\n\nStack Trace: {argEx.StackTrace}"
                    : "Um ou mais argumentos fornecidos são inválidos.",
                [argEx.Message]),

            UnauthorizedAccessException => CreateProblemDetails(
                HttpStatusCode.Unauthorized,
                "Não Autorizado",
                "Você não tem permissão para acessar este recurso.",
                ["Acesso negado."]),

            _ => CreateProblemDetails(
                HttpStatusCode.InternalServerError,
                isDevelopmentOrTesting ? exception.GetType().Name : "Erro Interno",
                isDevelopmentOrTesting
                    ? $"{exception.Message}\n\nStack Trace:\n{exception.StackTrace}"
                    : "Ocorreu um erro inesperado no servidor.",
                isDevelopmentOrTesting
                    ?
                    [
                        $"Exception Type: {exception.GetType().FullName}", exception.Message,
                        exception.InnerException?.Message ?? string.Empty
                    ]
                    : ["Erro inesperado. Entre em contato com o suporte."])
        };

        context.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.InternalServerError;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, JsonOptions));
    }

    private static ApiProblemDetails CreateProblemDetails(
        HttpStatusCode statusCode,
        string title,
        string detail,
        List<string> errors)
    {
        return new ApiProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Errors = errors
        };
    }
}
