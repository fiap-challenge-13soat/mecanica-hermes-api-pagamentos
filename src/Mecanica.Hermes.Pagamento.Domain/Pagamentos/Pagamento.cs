using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Events;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Status;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.ValueObjects;
using Mecanica.Hermes.Shared.Core.Domain;
using Mecanica.Hermes.Shared.Core.Results;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos;

/// <summary>
///     Agregado raiz que representa o ciclo de vida de um pagamento gerado para uma
///     <c>OrdemDeServico</c>. Coordena a criação do link no Mercado Pago, a reconciliação
///     por webhook e polling, e a publicação dos eventos de domínio que disparam o
///     callback à API de Ordem de Serviço.
/// </summary>
public class Pagamento : AggregateRoot
{
    /// <summary>
    ///     Limite default de tentativas de polling antes de considerar o pagamento expirado.
    /// </summary>
    public const int LimiteVerificacoesDefault = 15;

    private readonly List<PagamentoHistoricoStatus> _historicosStatus;

    private Pagamento(
        Guid id,
        Guid ordemDeServicoId,
        Guid clienteId,
        EmailCliente emailCliente,
        string nomeCliente,
        Dinheiro valor,
        DateTime? vencimento,
        PagamentoStatusBase statusAtual,
        int limiteVerificacoes,
        IEnumerable<PagamentoHistoricoStatus>? historicosStatus = null)
        : base(id)
    {
        OrdemDeServicoId = ordemDeServicoId;
        ClienteId = clienteId;
        EmailCliente = emailCliente;
        NomeCliente = nomeCliente;
        Valor = valor;
        Vencimento = vencimento;
        StatusAtual = statusAtual;
        LimiteVerificacoes = limiteVerificacoes;
        _historicosStatus = historicosStatus?.ToList() ?? [];
    }

    public Guid OrdemDeServicoId { get; private set; }
    public Guid ClienteId { get; private set; }
    public EmailCliente EmailCliente { get; private set; }
    public string NomeCliente { get; private set; }
    public Dinheiro Valor { get; private set; }
    public DateTime? Vencimento { get; private set; }
    public PagamentoStatusBase StatusAtual { get; private set; }
    public LinkPagamento? LinkPagamento { get; private set; }
    public string? UltimoMotivoFalha { get; private set; }
    public PagamentoConfirmadoVia? ConfirmadoVia { get; private set; }
    public int TentativasVerificacao { get; private set; }
    public int LimiteVerificacoes { get; private set; }

    public IReadOnlyCollection<PagamentoHistoricoStatus> HistoricosStatus => _historicosStatus;

    public bool PodeReverificar => TentativasVerificacao < LimiteVerificacoes &&
                                   !StatusAtual.IsTerminal;

    public static Result<Pagamento> Criar(
        Guid id,
        Guid ordemDeServicoId,
        Guid clienteId,
        string? emailCliente,
        string? nomeCliente,
        decimal valor,
        DateTime? vencimento = null,
        int limiteVerificacoes = LimiteVerificacoesDefault)
    {
        var errors = new List<string>();

        if (id == Guid.Empty)
            errors.Add("O Id do pagamento deve ser informado.");

        if (ordemDeServicoId == Guid.Empty)
            errors.Add("O OrdemDeServicoId deve ser informado.");

        if (clienteId == Guid.Empty)
            errors.Add("O ClienteId deve ser informado.");

        if (string.IsNullOrWhiteSpace(nomeCliente))
            errors.Add("O nome do cliente é obrigatório.");

        if (limiteVerificacoes <= 0)
            errors.Add("O limite de verificações precisa ser maior que zero.");

        var emailResult = EmailCliente.Criar(emailCliente);
        if (emailResult.IsFailure) errors.AddRange(emailResult.Errors);

        var valorResult = Dinheiro.Criar(valor);
        if (valorResult.IsFailure) errors.AddRange(valorResult.Errors);

        if (errors.Count > 0)
            return Result<Pagamento>.BadRequest(errors);

        var statusInicial = new PagamentoPendenteGeracao();
        var pagamento = new Pagamento(
            id,
            ordemDeServicoId,
            clienteId,
            emailResult.Data!,
            nomeCliente!.Trim(),
            valorResult.Data!,
            vencimento,
            statusInicial,
            limiteVerificacoes);

        pagamento.AddDomainEvent(new PagamentoCriadoEvent(
            pagamento.Id,
            pagamento.OrdemDeServicoId,
            pagamento.ClienteId,
            pagamento.EmailCliente.Valor,
            pagamento.Valor.Valor,
            pagamento.Valor.Moeda));

        return Result<Pagamento>.Ok(pagamento);
    }

    internal static Pagamento Restaurar(
        Guid id,
        Guid ordemDeServicoId,
        Guid clienteId,
        EmailCliente emailCliente,
        string nomeCliente,
        Dinheiro valor,
        DateTime? vencimento,
        PagamentoStatusBase statusAtual,
        LinkPagamento? linkPagamento,
        string? ultimoMotivoFalha,
        PagamentoConfirmadoVia? confirmadoVia,
        int tentativasVerificacao,
        int limiteVerificacoes,
        IEnumerable<PagamentoHistoricoStatus>? historicosStatus,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        var pagamento = new Pagamento(
            id,
            ordemDeServicoId,
            clienteId,
            emailCliente,
            nomeCliente,
            valor,
            vencimento,
            statusAtual,
            limiteVerificacoes,
            historicosStatus)
        {
            LinkPagamento = linkPagamento,
            UltimoMotivoFalha = ultimoMotivoFalha,
            ConfirmadoVia = confirmadoVia,
            TentativasVerificacao = tentativasVerificacao
        };
        pagamento.RestaurarBase(id, createdAt, updatedAt);
        return pagamento;
    }

    public Result GerarLink(string? preferenceId, string? url)
    {
        var linkResult = LinkPagamento.Criar(preferenceId, url);
        if (linkResult.IsFailure)
            return Result.BadRequest(linkResult.Errors);

        var statusAnterior = StatusAtual;
        var novoStatus = StatusAtual.GerarLink(AddDomainEvent);
        if (novoStatus.IsFailure)
            return Result.BadRequest(novoStatus.Errors);

        StatusAtual = novoStatus.Data!;
        LinkPagamento = linkResult.Data;
        ArquivarHistorico(statusAnterior);

        AddDomainEvent(new LinkPagamentoGeradoEvent(
            Id,
            OrdemDeServicoId,
            LinkPagamento!.PreferenceId,
            LinkPagamento.Url));

        MarkAsUpdated();
        return Result.Ok();
    }

    public Result MarcarEmailEnviado()
    {
        var statusAnterior = StatusAtual;
        var novoStatus = StatusAtual.MarcarEmailEnviado(AddDomainEvent);
        if (novoStatus.IsFailure)
            return Result.BadRequest(novoStatus.Errors);

        StatusAtual = novoStatus.Data!;
        ArquivarHistorico(statusAnterior);
        MarkAsUpdated();
        return Result.Ok();
    }

    public Result Confirmar(PagamentoConfirmadoVia via)
    {
        var statusAnterior = StatusAtual;
        var novoStatus = StatusAtual.Confirmar(via, AddDomainEvent);
        if (novoStatus.IsFailure)
            return Result.BadRequest(novoStatus.Errors);

        StatusAtual = novoStatus.Data!;
        ConfirmadoVia = via;
        ArquivarHistorico(statusAnterior);

        AddDomainEvent(new PagamentoConfirmadoEvent(Id, OrdemDeServicoId, via));

        MarkAsUpdated();
        return Result.Ok();
    }

    public Result Recusar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return Result.BadRequest("O motivo da recusa é obrigatório.");

        var statusAnterior = StatusAtual;
        var novoStatus = StatusAtual.Recusar(motivo, AddDomainEvent);
        if (novoStatus.IsFailure)
            return Result.BadRequest(novoStatus.Errors);

        StatusAtual = novoStatus.Data!;
        UltimoMotivoFalha = motivo.Trim();
        ArquivarHistorico(statusAnterior);

        AddDomainEvent(new PagamentoRecusadoEvent(Id, OrdemDeServicoId, UltimoMotivoFalha));

        MarkAsUpdated();
        return Result.Ok();
    }

    public Result Expirar()
    {
        var statusAnterior = StatusAtual;
        var novoStatus = StatusAtual.Expirar(AddDomainEvent);
        if (novoStatus.IsFailure)
            return Result.BadRequest(novoStatus.Errors);

        StatusAtual = novoStatus.Data!;
        ArquivarHistorico(statusAnterior);

        AddDomainEvent(new PagamentoExpiradoEvent(Id, OrdemDeServicoId));

        MarkAsUpdated();
        return Result.Ok();
    }

    /// <summary>
    ///     Incrementa o contador de polling. Não muda de estado por si — o caller decide
    ///     se deve <see cref="Expirar" /> ao atingir o limite ou agendar nova verificação.
    /// </summary>
    public Result<int> RegistrarTentativaVerificacao()
    {
        if (StatusAtual.IsTerminal)
            return Result<int>.BadRequest(
                "Pagamento em estado terminal não admite novas tentativas de verificação.");

        TentativasVerificacao++;
        MarkAsUpdated();
        return Result<int>.Ok(TentativasVerificacao);
    }

    private void ArquivarHistorico(PagamentoStatusBase statusAnterior)
    {
        _historicosStatus.Add(new PagamentoHistoricoStatus(
            statusAnterior,
            ResolverStatusAnteriorParaHistorico(statusAnterior)));
    }

    /// <summary>
    ///     Calcula o <c>StatusAnterior</c> a ser persistido no item de histórico que está
    ///     sendo arquivado, olhando o histórico já existente. Para o primeiro item registrado
    ///     (ainda não há histórico), retorna o próprio status sob transição — semântica de
    ///     "sem predecessor".
    /// </summary>
    private PagamentoStatus ResolverStatusAnteriorParaHistorico(PagamentoStatusBase statusSendoArquivado)
    {
        if (_historicosStatus.Count == 0)
            return statusSendoArquivado.StatusAtual;

        return _historicosStatus[^1].StatusAtual;
    }
}
