using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Shared.Core.Domain;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos;

public class PagamentoHistoricoStatus : BaseDomain, IPagamentoStatus
{
    private PagamentoHistoricoStatus()
    {
        // Constructor para o driver Mongo (deserialização)
    }

    /// <summary>
    ///     Cria um item de histórico arquivando o status sob transição.
    /// </summary>
    /// <param name="status">Status que está sendo arquivado (período encerrando).</param>
    /// <param name="statusAnterior">
    ///     Status que precedeu este na linha do tempo. Para o primeiro item do histórico,
    ///     passe o próprio <c>status.StatusAtual</c> indicando a ausência de predecessor.
    /// </param>
    internal PagamentoHistoricoStatus(IPagamentoStatus status, PagamentoStatus statusAnterior)
    {
        StatusAnterior = statusAnterior;
        StatusAtual = status.StatusAtual;
        StatusDestino = status.StatusDestino;
        DataInicio = status.DataInicio;
        DataFinalizacao = status.DataFinalizacao;
    }

    public PagamentoStatus StatusAnterior { get; internal set; }
    public PagamentoStatus StatusAtual { get; internal set; }
    public PagamentoStatus StatusDestino { get; internal set; }
    public DateTime DataInicio { get; internal set; }
    public DateTime? DataFinalizacao { get; internal set; }

    internal static PagamentoHistoricoStatus Restaurar(
        Guid id,
        PagamentoStatus statusAnterior,
        PagamentoStatus statusAtual,
        PagamentoStatus statusDestino,
        DateTime dataInicio,
        DateTime? dataFinalizacao,
        DateTime createdAt,
        DateTime? updatedAt)
    {
        var item = new PagamentoHistoricoStatus
        {
            StatusAnterior = statusAnterior,
            StatusAtual = statusAtual,
            StatusDestino = statusDestino,
            DataInicio = dataInicio,
            DataFinalizacao = dataFinalizacao
        };
        item.RestaurarBase(id, createdAt, updatedAt);
        return item;
    }
}
