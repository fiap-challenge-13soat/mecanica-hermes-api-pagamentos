namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

public interface IPagamentoMetrics
{
    void PagamentoCriado();
    void LinkGerado(double duracaoMillis);
    void PagamentoConfirmado(string via);
    void PagamentoRecusado();
    void PagamentoExpirado();
    void ErroRegistrado(string operacao, string tipoErro);
    void WebhookRecebido(string outcome);
    void MercadoPagoConsultaDuracao(double durationMillis);
}
