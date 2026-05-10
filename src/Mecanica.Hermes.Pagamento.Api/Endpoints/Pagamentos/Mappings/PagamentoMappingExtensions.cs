using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Contracts;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;

namespace Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Mappings;

public static class PagamentoMappingExtensions
{
    public static PagamentoResponse ToResponse(this PagamentoDto dto)
    {
        return new PagamentoResponse(
            dto.Id,
            dto.OrdemDeServicoId,
            dto.ClienteId,
            dto.EmailCliente,
            dto.NomeCliente,
            dto.Valor,
            dto.Moeda,
            dto.StatusAtual,
            dto.LinkPagamento is null
                ? null
                : new LinkPagamentoResponse(dto.LinkPagamento.PreferenceId, dto.LinkPagamento.Url),
            dto.UltimoMotivoFalha,
            dto.ConfirmadoVia,
            dto.TentativasVerificacao,
            dto.LimiteVerificacoes,
            dto.Vencimento,
            dto.HistoricosStatus
                .Select(h => new HistoricoStatusResponse(
                    h.Id,
                    h.StatusAnterior,
                    h.StatusAtual,
                    h.StatusDestino,
                    h.DataInicio,
                    h.DataFinalizacao))
                .ToList(),
            dto.CreatedAt,
            dto.UpdatedAt);
    }
}
