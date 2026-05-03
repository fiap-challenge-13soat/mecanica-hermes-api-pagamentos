namespace Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Contracts;

public sealed record PagamentoResponse(
    Guid Id,
    Guid OrdemDeServicoId,
    Guid ClienteId,
    string EmailCliente,
    string NomeCliente,
    decimal Valor,
    string Moeda,
    string StatusAtual,
    LinkPagamentoResponse? LinkPagamento,
    string? UltimoMotivoFalha,
    string? ConfirmadoVia,
    int TentativasVerificacao,
    int LimiteVerificacoes,
    DateTime? Vencimento,
    IList<HistoricoStatusResponse> HistoricosStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record LinkPagamentoResponse(string PreferenceId, string Url);

public sealed record HistoricoStatusResponse(
    Guid Id,
    string StatusAnterior,
    string StatusAtual,
    string StatusDestino,
    DateTime DataInicio,
    DateTime? DataFinalizacao);
