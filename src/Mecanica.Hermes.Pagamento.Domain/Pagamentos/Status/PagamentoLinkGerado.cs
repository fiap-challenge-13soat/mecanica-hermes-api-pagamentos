using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Common.Results;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Status;

public class PagamentoLinkGerado : PagamentoStatusBase
{
    public override PagamentoStatus StatusAtual => PagamentoStatus.LinkGerado;

    public override Result<PagamentoStatusBase> MarcarEmailEnviado(Action<IDomainEvent> addDomainEvent)
    {
        return AvancarPara(new PagamentoAguardandoConfirmacao());
    }

    public override Result<PagamentoStatusBase> Recusar(string motivo, Action<IDomainEvent> addDomainEvent)
    {
        return AvancarPara(new PagamentoRecusado());
    }

    public override Result<PagamentoStatusBase> Expirar(Action<IDomainEvent> addDomainEvent)
    {
        return AvancarPara(new PagamentoExpirado());
    }
}
