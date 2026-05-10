namespace Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Contracts;

public sealed record GerarPagamentoRequest(
    Guid OrdemDeServicoId,
    Guid ClienteId,
    string EmailCliente,
    string NomeCliente,
    decimal Valor,
    DateTime? Vencimento);
