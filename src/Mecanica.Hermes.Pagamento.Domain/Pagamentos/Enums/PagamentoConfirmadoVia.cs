namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

/// <summary>
///     Origem da confirmação do pagamento — usada para distinção em métricas e logs.
/// </summary>
public enum PagamentoConfirmadoVia
{
    Webhook,
    Polling
}
