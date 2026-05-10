using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Status;

public class PagamentoPago : PagamentoStatusBase
{
    public override PagamentoStatus StatusAtual => PagamentoStatus.Pago;
    public override bool IsTerminal => true;
}
