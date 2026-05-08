using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Shared.Core.Domain;
using Mecanica.Hermes.Shared.Core.Results;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Status;

public class PagamentoAguardandoConfirmacao : PagamentoStatusBase
{
    public override PagamentoStatus StatusAtual => PagamentoStatus.AguardandoConfirmacao;

    public override Result<PagamentoStatusBase> Confirmar(
        PagamentoConfirmadoVia via,
        Action<IDomainEvent> addDomainEvent)
    {
        return AvancarPara(new PagamentoPago());
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
