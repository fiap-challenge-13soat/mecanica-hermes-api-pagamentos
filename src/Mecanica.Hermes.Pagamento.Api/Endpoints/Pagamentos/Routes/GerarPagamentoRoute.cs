using System.Diagnostics.CodeAnalysis;
using System.Net;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Contracts;
using Mecanica.Hermes.Pagamento.Api.Presenter;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Commands.GerarPagamento;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;
using Mecanica.Hermes.Shared.AspNetCore.Endpoints;
using Mecanica.Hermes.Shared.AspNetCore.Filters;
using Mecanica.Hermes.Shared.AspNetCore.Presenter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Routes;

[ExcludeFromCodeCoverage(Justification = "Definição de rota minimal API — coberta por testes de integração com WebApplicationFactory.")]
public static class GerarPagamentoRoute
{
    public static void MapGerarPagamento(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/", GerarPagamento)
            .WithName("GerarPagamento")
            .ProducesAcceptedCommand<GerarPagamentoAcceptedResponse>()
            .RequireAuthorization(AuthPolicies.OnlyAdminScope)
            .WithDescription(
                "Solicita a criação de um pagamento (link no Mercado Pago + envio de e-mail). " +
                "Processado de forma assíncrona via SAGA — retorna 202 com o Id gerado.")
            .AddEndpointFilter<ValidationFilter<GerarPagamentoRequest>>();
    }

    private static async Task<IResult> GerarPagamento(
        [FromBody] GerarPagamentoRequest request,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        var pagamentoId = Guid.NewGuid();

        var command = new GerarPagamentoCommand(
            pagamentoId,
            request.OrdemDeServicoId,
            request.ClienteId,
            request.EmailCliente,
            request.NomeCliente,
            request.Valor,
            request.Vencimento);

        var response = await sender.Send(command, cancellationToken);

        if (!response.IsSuccess)
            return response.Present();

        if (response.StatusCode != HttpStatusCode.Accepted)
            return response.Present();

        return Results.Accepted(
            $"/api/pagamentos/{pagamentoId}",
            new GerarPagamentoAcceptedResponse(pagamentoId));
    }
}
