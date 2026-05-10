using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Routes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos;

[ExcludeFromCodeCoverage(Justification = "Composição de rotas — só MapGroup + MapXxx().")]
public static class PagamentosEndpoints
{
    public static void MapPagamentosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pagamentos")
            .WithTags("Pagamentos");

        group.MapGerarPagamento();
        group.MapConsultarPagamento();
        group.MapVerificarStatus();
    }
}
