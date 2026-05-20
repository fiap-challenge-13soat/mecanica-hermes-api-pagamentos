using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Webhooks.Contracts;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Constants;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Infrastructure.MercadoPago;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Mecanica.Hermes.Pagamento.Api.Endpoints.Webhooks;

[ExcludeFromCodeCoverage(Justification =
    "Definição de rota minimal API + handler — coberta por testes de integração com WireMock.")]
public static partial class MercadoPagoWebhookRoute
{
    public static void MapMercadoPagoWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/mercadopago", HandleWebhook)
            .WithName("MercadoPagoWebhook")
            .WithTags("Webhooks")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest)
            .AllowAnonymous()
            .WithDescription(
                "Recebe notificações do Mercado Pago. Valida HMAC, deduplica por mp_event_id e " +
                "publica MercadoPagoCallbackRecebidoRequest no bus para processamento assíncrono.");
    }

    private static async Task<IResult> HandleWebhook(
        [FromBody] MercadoPagoWebhookPayload payload,
        [FromServices] WebhookSignatureValidator signatureValidator,
        [FromServices] IWebhookEventRepository webhookEvents,
        [FromServices] IPublishEndpoint publishEndpoint,
        [FromServices] IPagamentoMetrics metrics,
        [FromServices] ILogger<MercadoPagoWebhookPayload> logger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var signature = httpContext.Request.Headers["x-signature"].ToString();
        var requestId = httpContext.Request.Headers["x-request-id"].ToString();
        var dataId = payload?.Data?.Id ?? string.Empty;

        if (!signatureValidator.Validate(signature, requestId, dataId))
        {
            metrics.ErroRegistrado(
                PagamentoMetricKeys.Operations.ConfirmarWebhook,
                PagamentoMetricKeys.ErrorTypes.WebhookSignatureInvalid);
            LogAssinaturaInvalida(logger);
            return Results.Unauthorized();
        }

        if (!long.TryParse(dataId, out var mercadoPagoPaymentId))
        {
            // No mundo real o id do payment do MP precisa ser convertido em PagamentoId via
            // external_reference (chamada extra ao MP). Para simplificar nesse scaffold, aceitamos
            // que `data.id` é o próprio PagamentoId — o cliente envia preference com
            // external_reference = PagamentoId, e o webhook deve carregar esse identificador.
            LogDataIdNaoLong(logger, dataId);
            return Results.BadRequest();
        }

        // Scaffold: deriva um Guid determinístico do MP payment ID para correlação da SAGA.
        // Em produção, o PagamentoId real deve ser obtido via external_reference na API do MP.
        var bytes = new byte[16];
        BitConverter.GetBytes(mercadoPagoPaymentId).CopyTo(bytes, 0);
        var pagamentoId = new Guid(bytes);

        var status = payload?.Action ?? "pending";

        var registered = await webhookEvents.TryRegisterAsync(
            mercadoPagoEventId: requestId.Length > 0 ? requestId : Guid.NewGuid().ToString(),
            pagamentoId: pagamentoId,
            status: status,
            cancellationToken: cancellationToken);

        if (!registered)
        {
            metrics.WebhookRecebido(PagamentoMetricKeys.WebhookOutcomes.Duplicate);
            return Results.Accepted();
        }

        await publishEndpoint.Publish(
            new MercadoPagoCallbackRecebidoRequest(pagamentoId, requestId, status),
            cancellationToken);

        return Results.Accepted();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook do Mercado Pago recebido com assinatura inválida.")]
    private static partial void LogAssinaturaInvalida(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook do Mercado Pago recebeu data.id não-long: {DataId}")]
    private static partial void LogDataIdNaoLong(ILogger logger, string dataId);
}
