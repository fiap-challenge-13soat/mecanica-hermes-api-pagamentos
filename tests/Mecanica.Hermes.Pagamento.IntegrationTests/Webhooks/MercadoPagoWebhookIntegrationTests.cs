using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using Mecanica.Hermes.Pagamento.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Mecanica.Hermes.Pagamento.IntegrationTests.Webhooks;

[Collection(IntegrationTestCollection.Name)]
public sealed class MercadoPagoWebhookIntegrationTests(IntegrationTestWebApplicationFactory factory)
{
    [Fact]
    public async Task Post_SemAssinatura_RetornaUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var pagamentoId = Guid.NewGuid();
        var payload = NewPayload(pagamentoId);

        var resp = await client.PostAsJsonAsync("/api/webhooks/mercadopago", payload);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AssinaturaInvalida_RetornaUnauthorized()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var pagamentoId = Guid.NewGuid();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/mercadopago")
        {
            Content = JsonContent.Create(NewPayload(pagamentoId))
        };
        req.Headers.Add("x-request-id", "evt-bad");
        req.Headers.Add("x-signature", $"ts={ts},v1=deadbeef");

        var resp = await client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AssinaturaValida_RetornaAcceptedERegistraEvento()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var pagamentoId = Guid.NewGuid();
        var requestId = "evt-" + Guid.NewGuid().ToString("N");
        var payload = NewPayload(pagamentoId);

        using var req = BuildSignedRequest(payload, pagamentoId, requestId);
        var resp = await client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var registrado = await ctx.WebhookEvents
            .Find(d => d.MercadoPagoEventId == requestId)
            .FirstOrDefaultAsync();
        registrado.Should().NotBeNull();
        registrado!.PagamentoId.Should().Be(pagamentoId);
        registrado.Status.Should().Be("payment.updated");
    }

    [Fact]
    public async Task Post_RetransmissaoComMesmoRequestId_NaoDuplicaERetornaAccepted()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var pagamentoId = Guid.NewGuid();
        var requestId = "evt-dup-" + Guid.NewGuid().ToString("N");
        var payload = NewPayload(pagamentoId);

        using var req1 = BuildSignedRequest(payload, pagamentoId, requestId);
        using var req2 = BuildSignedRequest(payload, pagamentoId, requestId);

        var resp1 = await client.SendAsync(req1);
        var resp2 = await client.SendAsync(req2);

        resp1.StatusCode.Should().Be(HttpStatusCode.Accepted);
        resp2.StatusCode.Should().Be(HttpStatusCode.Accepted, "retransmissões viram no-op idempotente");

        using var scope = factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var count = await ctx.WebhookEvents.CountDocumentsAsync(
            Builders<WebhookEventDocument>.Filter.Eq(d => d.MercadoPagoEventId, requestId));
        count.Should().Be(1, "idempotência via _id deve impedir double-insert");
    }

    [Fact]
    public async Task Post_DataIdNaoGuid_RetornaBadRequest()
    {
        await factory.ResetDatabaseAsync();
        var client = factory.CreateClient();

        var requestId = "evt-bad-id-" + Guid.NewGuid().ToString("N");
        var payload = new { type = "payment", action = "payment.updated", data = new { id = "nao-eh-guid" } };

        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var manifest = $"id:nao-eh-guid;request-id:{requestId};ts:{ts};";
        var hmac = ComputeHmac(manifest, IntegrationTestWebApplicationFactory.WebhookSecret);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/mercadopago")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Add("x-request-id", requestId);
        req.Headers.Add("x-signature", $"ts={ts},v1={hmac}");

        var resp = await client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static object NewPayload(Guid pagamentoId) =>
        new { type = "payment", action = "payment.updated", data = new { id = pagamentoId.ToString() } };

    private static HttpRequestMessage BuildSignedRequest(object payload, Guid pagamentoId, string requestId)
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var dataId = pagamentoId.ToString();
        var manifest = $"id:{dataId};request-id:{requestId};ts:{ts};";
        var hmac = ComputeHmac(manifest, IntegrationTestWebApplicationFactory.WebhookSecret);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/mercadopago")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Add("x-request-id", requestId);
        req.Headers.Add("x-signature", $"ts={ts},v1={hmac}");
        return req;
    }

    private static string ComputeHmac(string manifest, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
