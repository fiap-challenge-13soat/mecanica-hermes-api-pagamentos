using System.Text.Json;
using FluentAssertions;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Contracts;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Webhooks.Contracts;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Api.Tests.Endpoints;

public class ContractsTests
{
    [Fact]
    public void GerarPagamentoRequest_CarriesAllProperties()
    {
        var ordem = Guid.NewGuid();
        var cliente = Guid.NewGuid();
        var venc = DateTime.UtcNow.AddDays(2);

        var req = new GerarPagamentoRequest(ordem, cliente, "x@y.com", "Joao", 1.5m, venc);

        req.OrdemDeServicoId.Should().Be(ordem);
        req.ClienteId.Should().Be(cliente);
        req.EmailCliente.Should().Be("x@y.com");
        req.NomeCliente.Should().Be("Joao");
        req.Valor.Should().Be(1.5m);
        req.Vencimento.Should().Be(venc);
    }

    [Fact]
    public void GerarPagamentoAcceptedResponse_StoresId()
    {
        var id = Guid.NewGuid();

        var resp = new GerarPagamentoAcceptedResponse(id);

        resp.Id.Should().Be(id);
    }

    [Fact]
    public void PagamentoResponse_RecordEqualityIsByValue_ForScalarFields()
    {
        // PagamentoResponse holds an IList<HistoricoStatusResponse>; record-generated equality on
        // collections is reference-based, so we only assert structural equality on scalar fields.
        var link = new LinkPagamentoResponse("pref", "https://x");
        var sharedHistoricos = new List<HistoricoStatusResponse>
        {
            new(Guid.Empty, "A", "B", "C", DateTime.UnixEpoch, null)
        };

        var a = new PagamentoResponse(
            Guid.Empty, Guid.Empty, Guid.Empty, "e", "n", 1m, "BRL", "X",
            link, null, null, 0, 1, null, sharedHistoricos, DateTime.UnixEpoch, null);
        var b = new PagamentoResponse(
            Guid.Empty, Guid.Empty, Guid.Empty, "e", "n", 1m, "BRL", "X",
            link, null, null, 0, 1, null, sharedHistoricos, DateTime.UnixEpoch, null);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void LinkPagamentoResponse_RecordEqualityIsByValue()
    {
        var a = new LinkPagamentoResponse("pref", "https://x");
        var b = new LinkPagamentoResponse("pref", "https://x");

        a.Should().Be(b);
    }

    [Fact]
    public void HistoricoStatusResponse_RecordEqualityIsByValue()
    {
        var id = Guid.NewGuid();
        var dt = DateTime.UnixEpoch;
        var a = new HistoricoStatusResponse(id, "A", "B", "C", dt, null);
        var b = new HistoricoStatusResponse(id, "A", "B", "C", dt, null);

        a.Should().Be(b);
    }

    [Fact]
    public void MercadoPagoWebhookPayload_DeserializesExpectedShape()
    {
        const string json = "{\"type\":\"payment\",\"action\":\"payment.created\",\"data\":{\"id\":\"123\"}}";

        var payload = JsonSerializer.Deserialize<MercadoPagoWebhookPayload>(json);

        payload.Should().NotBeNull();
        payload!.Type.Should().Be("payment");
        payload.Action.Should().Be("payment.created");
        payload.Data.Should().NotBeNull();
        payload.Data!.Id.Should().Be("123");
    }

    [Fact]
    public void MercadoPagoWebhookPayload_WithMissingFields_DefaultsToNull()
    {
        const string json = "{}";

        var payload = JsonSerializer.Deserialize<MercadoPagoWebhookPayload>(json);

        payload.Should().NotBeNull();
        payload!.Type.Should().BeNull();
        payload.Data.Should().BeNull();
    }
}
