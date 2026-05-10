using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using FluentAssertions;
using Mecanica.Hermes.Pagamento.Infrastructure.Observability;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.Observability;

public class PagamentoMetricsTests
{
    private static (PagamentoMetrics metrics, MeterListener listener,
            ConcurrentBag<(string Instrument, long Value, KeyValuePair<string, object?>[] Tags)> longRecords,
            ConcurrentBag<(string Instrument, double Value, KeyValuePair<string, object?>[] Tags)> doubleRecords)
        BuildMetricsWithListener()
    {
        var metrics = new PagamentoMetrics();

        var longRecords = new ConcurrentBag<(string, long, KeyValuePair<string, object?>[])>();
        var doubleRecords = new ConcurrentBag<(string, double, KeyValuePair<string, object?>[])>();

        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == PagamentoMetrics.MeterName)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            longRecords.Add((instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            doubleRecords.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();

        return (metrics, listener, longRecords, doubleRecords);
    }

    [Fact]
    public void PagamentoCriado_RecordsCounter()
    {
        var (metrics, listener, longs, _) = BuildMetricsWithListener();
        using var _l = listener;
        using var _m = metrics;

        metrics.PagamentoCriado();

        longs.Should().ContainSingle(r => r.Instrument == "pagamento.criado" && r.Value == 1);
    }

    [Fact]
    public void PagamentoConfirmado_RecordsCounterWithViaTag()
    {
        var (metrics, listener, longs, _) = BuildMetricsWithListener();
        using var _l = listener;
        using var _m = metrics;

        metrics.PagamentoConfirmado("webhook");

        longs.Should().ContainSingle(r =>
            r.Instrument == "pagamento.confirmado"
            && r.Value == 1
            && r.Tags.Any(t => t.Key == "via" && (string?)t.Value == "webhook"));
    }

    [Fact]
    public void PagamentoRecusado_RecordsCounter()
    {
        var (metrics, listener, longs, _) = BuildMetricsWithListener();
        using var _l = listener;
        using var _m = metrics;

        metrics.PagamentoRecusado();

        longs.Should().ContainSingle(r => r.Instrument == "pagamento.recusado" && r.Value == 1);
    }

    [Fact]
    public void PagamentoExpirado_RecordsCounter()
    {
        var (metrics, listener, longs, _) = BuildMetricsWithListener();
        using var _l = listener;
        using var _m = metrics;

        metrics.PagamentoExpirado();

        longs.Should().ContainSingle(r => r.Instrument == "pagamento.expirado" && r.Value == 1);
    }

    [Fact]
    public void ErroRegistrado_RecordsCounterWithOperacaoAndTipoErroTags()
    {
        var (metrics, listener, longs, _) = BuildMetricsWithListener();
        using var _l = listener;
        using var _m = metrics;

        metrics.ErroRegistrado("gerar-link", "Timeout");

        var match = longs.SingleOrDefault(r => r.Instrument == "pagamento.erros");
        match.Value.Should().Be(1);
        match.Tags.Should().Contain(t => t.Key == "operacao" && (string?)t.Value == "gerar-link");
        match.Tags.Should().Contain(t => t.Key == "tipo_erro" && (string?)t.Value == "Timeout");
    }

    [Fact]
    public void WebhookRecebido_RecordsCounterWithOutcomeTag()
    {
        var (metrics, listener, longs, _) = BuildMetricsWithListener();
        using var _l = listener;
        using var _m = metrics;

        metrics.WebhookRecebido("processed");

        longs.Should().ContainSingle(r =>
            r.Instrument == "mercado_pago.webhook.recebido"
            && r.Tags.Any(t => t.Key == "outcome" && (string?)t.Value == "processed"));
    }

    [Fact]
    public void LinkGerado_RecordsHistogram()
    {
        var (metrics, listener, _, doubles) = BuildMetricsWithListener();
        using var _l = listener;
        using var _m = metrics;

        metrics.LinkGerado(123.4);

        doubles.Should().ContainSingle(r =>
            r.Instrument == "mercado_pago.preference.criada.duracao_ms"
            && Math.Abs(r.Value - 123.4) < 0.0001);
    }

    [Fact]
    public void MercadoPagoConsultaDuracao_RecordsHistogram()
    {
        var (metrics, listener, _, doubles) = BuildMetricsWithListener();
        using var _l = listener;
        using var _m = metrics;

        metrics.MercadoPagoConsultaDuracao(56.78);

        doubles.Should().ContainSingle(r =>
            r.Instrument == "mercado_pago.consulta.duracao_ms"
            && Math.Abs(r.Value - 56.78) < 0.0001);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var metrics = new PagamentoMetrics();

        Action act = () =>
        {
            metrics.Dispose();
            metrics.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void MeterName_IsExpectedConstant()
    {
        PagamentoMetrics.MeterName.Should().Be("MecanicaHermes.Pagamento");
    }
}
