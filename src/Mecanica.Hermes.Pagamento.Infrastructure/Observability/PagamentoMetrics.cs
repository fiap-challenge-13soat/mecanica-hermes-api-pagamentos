using System.Diagnostics.Metrics;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Observability;

internal sealed class PagamentoMetrics : IPagamentoMetrics, IDisposable
{
    public const string MeterName = "MecanicaHermes.Pagamento";

    private readonly Counter<long> _criados;
    private readonly Counter<long> _confirmados;
    private readonly Counter<long> _recusados;
    private readonly Counter<long> _expirados;
    private readonly Counter<long> _erros;
    private readonly Counter<long> _webhooks;
    private readonly Histogram<double> _linkGeradoDuracao;
    private readonly Histogram<double> _consultaMpDuracao;

    private readonly Meter _meter;

    public PagamentoMetrics()
    {
        _meter = new Meter(MeterName);
        _criados = _meter.CreateCounter<long>("pagamento.criado", description: "Pagamentos criados.");
        _confirmados = _meter.CreateCounter<long>("pagamento.confirmado",
            description: "Pagamentos confirmados (label via=webhook|polling).");
        _recusados = _meter.CreateCounter<long>("pagamento.recusado", description: "Pagamentos recusados.");
        _expirados = _meter.CreateCounter<long>("pagamento.expirado", description: "Pagamentos expirados.");
        _erros = _meter.CreateCounter<long>("pagamento.erros",
            description: "Erros registrados em consumers/handlers (labels operacao, tipo_erro).");
        _webhooks = _meter.CreateCounter<long>("mercado_pago.webhook.recebido",
            description: "Webhooks do Mercado Pago recebidos (label outcome).");
        _linkGeradoDuracao = _meter.CreateHistogram<double>("mercado_pago.preference.criada.duracao_ms",
            description: "Duração da chamada de criação de preference no Mercado Pago.");
        _consultaMpDuracao = _meter.CreateHistogram<double>("mercado_pago.consulta.duracao_ms",
            description: "Duração da consulta de status no Mercado Pago.");
    }

    public void PagamentoCriado()
    {
        _criados.Add(1);
    }

    public void LinkGerado(double duracaoMillis)
    {
        _linkGeradoDuracao.Record(duracaoMillis);
    }

    public void PagamentoConfirmado(string via)
    {
        _confirmados.Add(1, new KeyValuePair<string, object?>("via", via));
    }

    public void PagamentoRecusado()
    {
        _recusados.Add(1);
    }

    public void PagamentoExpirado()
    {
        _expirados.Add(1);
    }

    public void ErroRegistrado(string operacao, string tipoErro)
    {
        _erros.Add(1,
            new KeyValuePair<string, object?>("operacao", operacao),
            new KeyValuePair<string, object?>("tipo_erro", tipoErro));
    }

    public void WebhookRecebido(string outcome)
    {
        _webhooks.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }

    public void MercadoPagoConsultaDuracao(double durationMillis)
    {
        _consultaMpDuracao.Record(durationMillis);
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
