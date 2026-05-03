namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Dtos;

public sealed record PagamentoDto(
    Guid Id,
    Guid OrdemDeServicoId,
    Guid ClienteId,
    string EmailCliente,
    string NomeCliente,
    decimal Valor,
    string Moeda,
    string StatusAtual,
    LinkPagamentoDto? LinkPagamento,
    string? UltimoMotivoFalha,
    string? ConfirmadoVia,
    int TentativasVerificacao,
    int LimiteVerificacoes,
    DateTime? Vencimento,
    IList<PagamentoHistoricoStatusDto> HistoricosStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record LinkPagamentoDto(string PreferenceId, string Url);

public sealed record PagamentoHistoricoStatusDto(
    Guid Id,
    string StatusAnterior,
    string StatusAtual,
    string StatusDestino,
    DateTime DataInicio,
    DateTime? DataFinalizacao);
