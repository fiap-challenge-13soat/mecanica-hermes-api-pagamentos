using System.Diagnostics;
using Mecanica.Hermes.Pagamento.Domain.Common.Results;
using MediatR;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1873 // Log arguments are simple local variables — not expensive

namespace Mecanica.Hermes.Pagamento.Application.Common.Behaviors;

/// <summary>
///     Pipeline behavior que registra o início e o término de cada request MediatR,
///     incluindo duração e status (sucesso/falha).
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        logger.LogInformation("Handling {RequestName}", requestName);

        var response = await next(cancellationToken);

        sw.Stop();

        if (response.IsSuccess)
            logger.LogDebug(
                "Handled {RequestName} in {ElapsedMs}ms — Success",
                requestName, sw.ElapsedMilliseconds);
        else
            logger.LogWarning(
                "Handled {RequestName} in {ElapsedMs}ms — Failure: {Errors}",
                requestName, sw.ElapsedMilliseconds, response.Errors);

        return response;
    }
}
