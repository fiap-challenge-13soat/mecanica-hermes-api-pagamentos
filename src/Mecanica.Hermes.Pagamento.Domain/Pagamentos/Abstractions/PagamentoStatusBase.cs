using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Common.Clock;
using Mecanica.Hermes.Pagamento.Domain.Common.Results;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Abstractions;

/// <summary>
///     Implementação do State Pattern para o ciclo de vida do <c>Pagamento</c>.
///     Cada operação retorna o próximo estado válido (ou um <see cref="Result{T}" />
///     em falha). O agregado encapsula a transição via métodos públicos
///     (<c>GerarLink</c>, <c>MarcarEmailEnviado</c>, <c>Confirmar</c>, <c>Recusar</c>,
///     <c>Expirar</c>) e atualiza <see cref="StatusDestino" />, <see cref="DataFinalizacao" />.
/// </summary>
public abstract class PagamentoStatusBase : BaseDomain, IPagamentoStatus
{
    public abstract PagamentoStatus StatusAtual { get; }
    public PagamentoStatus StatusDestino { get; internal set; }

    public DateTime DataInicio { get; internal set; } = DomainClock.UtcNow;
    public DateTime? DataFinalizacao { get; internal set; }

    /// <summary>
    ///     Indica se o estado representa o fim do ciclo (não admite novas transições).
    /// </summary>
    public virtual bool IsTerminal => false;

    /// <summary>
    ///     Gera o link no Mercado Pago. Default: rejeita com mensagem indicando estado inválido.
    ///     Override em <see cref="Status.PagamentoPendenteGeracao" /> para permitir a transição.
    /// </summary>
    public virtual Result<PagamentoStatusBase> GerarLink(Action<IDomainEvent> addDomainEvent)
    {
        return Result<PagamentoStatusBase>.BadRequest(
            $"Pagamento em status '{StatusAtual}' não pode gerar link.");
    }

    /// <summary>
    ///     Marca que o e-mail com o link de pagamento foi enfileirado. Default: rejeita.
    ///     Override em <see cref="Status.PagamentoLinkGerado" />.
    /// </summary>
    public virtual Result<PagamentoStatusBase> MarcarEmailEnviado(Action<IDomainEvent> addDomainEvent)
    {
        return Result<PagamentoStatusBase>.BadRequest(
            $"Pagamento em status '{StatusAtual}' não pode marcar e-mail enviado.");
    }

    /// <summary>
    ///     Confirma o pagamento (via webhook ou polling). Default: rejeita.
    ///     Override em <see cref="Status.PagamentoAguardandoConfirmacao" />.
    /// </summary>
    public virtual Result<PagamentoStatusBase> Confirmar(
        PagamentoConfirmadoVia via,
        Action<IDomainEvent> addDomainEvent)
    {
        return Result<PagamentoStatusBase>.BadRequest(
            $"Pagamento em status '{StatusAtual}' não pode ser confirmado.");
    }

    /// <summary>
    ///     Recusa o pagamento (motivo do MP ou erro). Default: rejeita.
    ///     Override em <see cref="Status.PagamentoAguardandoConfirmacao" /> e
    ///     <see cref="Status.PagamentoLinkGerado" />.
    /// </summary>
    public virtual Result<PagamentoStatusBase> Recusar(string motivo, Action<IDomainEvent> addDomainEvent)
    {
        return Result<PagamentoStatusBase>.BadRequest(
            $"Pagamento em status '{StatusAtual}' não pode ser recusado.");
    }

    /// <summary>
    ///     Expira o pagamento (timeout do polling ou vencimento). Default: rejeita.
    ///     Override em estados não terminais.
    /// </summary>
    public virtual Result<PagamentoStatusBase> Expirar(Action<IDomainEvent> addDomainEvent)
    {
        return Result<PagamentoStatusBase>.BadRequest(
            $"Pagamento em status '{StatusAtual}' não pode ser expirado.");
    }

    /// <summary>
    ///     Rehidrata o estado completo a partir da persistência. Uso restrito ao mapper
    ///     de infraestrutura — não chame em código de domínio/aplicação.
    /// </summary>
    internal void Restaurar(
        Guid id,
        PagamentoStatus statusDestino,
        DateTime dataInicio,
        DateTime? dataFinalizacao,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        RestaurarBase(id, createdAt, updatedAt);
        StatusDestino = statusDestino;
        DataInicio = dataInicio;
        DataFinalizacao = dataFinalizacao;
    }

    /// <summary>
    ///     Auxiliar para concretos: encerra a janela atual (DataFinalizacao + StatusDestino) e
    ///     devolve o próximo estado embrulhado em <see cref="Result{T}" />.
    /// </summary>
    protected Result<PagamentoStatusBase> AvancarPara(PagamentoStatusBase proximo)
    {
        DataFinalizacao = DomainClock.UtcNow;
        StatusDestino = proximo.StatusAtual;
        return Result<PagamentoStatusBase>.Ok(proximo);
    }
}
