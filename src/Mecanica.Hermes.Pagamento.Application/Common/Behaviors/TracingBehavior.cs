using System.Diagnostics;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Commands;
using Mecanica.Hermes.Pagamento.Domain.Common.Results;
using MediatR;

namespace Mecanica.Hermes.Pagamento.Application.Common.Behaviors;

/// <summary>
///     Pipeline behavior que cria um <see cref="Activity" /> OpenTelemetry para cada request
///     MediatR, propagando o nome do request e o <c>PagamentoId</c> quando disponível.
/// </summary>
public sealed class TracingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private static readonly ActivitySource Source =
        new("MecanicaHermes.Pagamento.Application",
            typeof(TracingBehavior<,>).Assembly.GetName().Version?.ToString() ?? "1.0");

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        using var activity = Source.StartActivity(requestName);

        activity?.SetTag("mediator.request", requestName);

        if (request is IPagamentoCommand pagamentoCommand)
            activity?.SetTag("pagamento.id", pagamentoCommand.PagamentoId.ToString());

        var response = await next(cancellationToken);

        activity?.SetStatus(response.IsSuccess ? ActivityStatusCode.Ok : ActivityStatusCode.Error);

        return response;
    }
}
