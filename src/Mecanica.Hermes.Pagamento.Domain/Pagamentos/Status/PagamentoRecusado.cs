using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Status;

public class PagamentoRecusado : PagamentoStatusBase
{
    public override PagamentoStatus StatusAtual => PagamentoStatus.Recusado;
    public override bool IsTerminal => true;
}
