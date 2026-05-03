using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Api.Presenter;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Commands.VerificarStatus;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Routes;

[ExcludeFromCodeCoverage(Justification = "Definição de rota minimal API — coberta por testes de integração com WebApplicationFactory.")]
public static class VerificarStatusRoute
{
    public static void MapVerificarStatus(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}/status", VerificarStatus)
            .WithName("VerificarStatusPagamento")
            .ProducesAcceptedCommand()
            .RequireAuthorization(AuthPolicies.OnlyAdminScope)
            .WithDescription(
                "Força uma reconciliação imediata do status do pagamento no Mercado Pago. " +
                "Endpoint operacional/diagnóstico — idempotente.");
    }

    private static async Task<IResult> VerificarStatus(
        [FromRoute] Guid id,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new VerificarStatusPagamentoCommand(id);
        var response = await sender.Send(command, cancellationToken);
        return response.Present();
    }
}
