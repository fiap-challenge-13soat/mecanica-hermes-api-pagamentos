using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Common.Results;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Status;

public class PagamentoPendenteGeracao : PagamentoStatusBase
{
    public override PagamentoStatus StatusAtual => PagamentoStatus.PendenteGeracao;

    public override Result<PagamentoStatusBase> GerarLink(Action<IDomainEvent> addDomainEvent)
    {
        return AvancarPara(new PagamentoLinkGerado());
    }

    public override Result<PagamentoStatusBase> Expirar(Action<IDomainEvent> addDomainEvent)
    {
        return AvancarPara(new PagamentoExpirado());
    }
}
