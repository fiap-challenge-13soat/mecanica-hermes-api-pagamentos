using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Api.Presenter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Mecanica.Hermes.Pagamento.Api.Endpoints;

[ExcludeFromCodeCoverage(Justification = "Endpoint metadata helpers — composição/decoração, sem lógica.")]
internal static class EndpointConventions
{
    public static RouteHandlerBuilder ProducesStandardErrors(this RouteHandlerBuilder builder)
    {
        return builder
            .Produces<ApiProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ApiProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ApiProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ApiProblemDetails>(StatusCodes.Status500InternalServerError);
    }

    public static RouteHandlerBuilder ProducesAcceptedCommand(this RouteHandlerBuilder builder)
    {
        return builder
            .Produces(StatusCodes.Status202Accepted)
            .ProducesStandardErrors();
    }

    public static RouteHandlerBuilder ProducesAcceptedCommand<TAcceptedBody>(this RouteHandlerBuilder builder)
    {
        return builder
            .Produces<TAcceptedBody>(StatusCodes.Status202Accepted)
            .ProducesStandardErrors();
    }

    public static RouteHandlerBuilder ProducesQuerySingle<TBody>(this RouteHandlerBuilder builder)
    {
        return builder
            .Produces<TBody>()
            .Produces<ApiProblemDetails>(StatusCodes.Status404NotFound)
            .ProducesStandardErrors();
    }
}
