namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

public enum PagamentoStatus
{
    PendenteGeracao,
    LinkGerado,
    AguardandoConfirmacao,
    Pago,
    Recusado,
    Expirado
}
