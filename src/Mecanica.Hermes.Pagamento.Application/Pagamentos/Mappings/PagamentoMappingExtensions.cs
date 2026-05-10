using Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Mappings;

public static class PagamentoMappingExtensions
{
    public static PagamentoDto ToDto(this Domain.Pagamentos.Pagamento pagamento)
    {
        return new PagamentoDto(
            pagamento.Id,
            pagamento.OrdemDeServicoId,
            pagamento.ClienteId,
            pagamento.EmailCliente.Valor,
            pagamento.NomeCliente,
            pagamento.Valor.Valor,
            pagamento.Valor.Moeda,
            pagamento.StatusAtual.StatusAtual.ToString(),
            pagamento.LinkPagamento is null
                ? null
                : new LinkPagamentoDto(pagamento.LinkPagamento.PreferenceId, pagamento.LinkPagamento.Url),
            pagamento.UltimoMotivoFalha,
            pagamento.ConfirmadoVia?.ToString(),
            pagamento.TentativasVerificacao,
            pagamento.LimiteVerificacoes,
            pagamento.Vencimento,
            pagamento.HistoricosStatus
                .Select(h => new PagamentoHistoricoStatusDto(
                    h.Id,
                    h.StatusAnterior.ToString(),
                    h.StatusAtual.ToString(),
                    h.StatusDestino.ToString(),
                    h.DataInicio,
                    h.DataFinalizacao))
                .ToList(),
            pagamento.CreatedAt,
            pagamento.UpdatedAt);
    }
}
