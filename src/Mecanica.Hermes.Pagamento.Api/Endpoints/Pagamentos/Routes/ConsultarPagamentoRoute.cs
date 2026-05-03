using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Contracts;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Mappings;
using Mecanica.Hermes.Pagamento.Api.Presenter;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Queries.GetPagamentoById;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Routes;

[ExcludeFromCodeCoverage(Justification = "Definição de rota minimal API — coberta por testes de integração com WebApplicationFactory.")]
public static class ConsultarPagamentoRoute
{
    public static void MapConsultarPagamento(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{id:guid}", ConsultarPagamento)
            .WithName("ConsultarPagamento")
            .ProducesQuerySingle<PagamentoResponse>()
            .RequireAuthorization(AuthPolicies.AllowClienteScope)
            .WithDescription("Busca um pagamento pelo Id (geralmente o mesmo Id usado pela API de OS).");
    }

    private static async Task<IResult> ConsultarPagamento(
        [FromRoute] Guid id,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetPagamentoByIdQuery(id);
        var response = await sender.Send(query, cancellationToken);
        return response.Present(dto => dto.ToResponse());
    }
}
