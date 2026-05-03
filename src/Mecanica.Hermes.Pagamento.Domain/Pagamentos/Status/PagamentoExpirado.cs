using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Status;

public class PagamentoExpirado : PagamentoStatusBase
{
    public override PagamentoStatus StatusAtual => PagamentoStatus.Expirado;
    public override bool IsTerminal => true;
}
