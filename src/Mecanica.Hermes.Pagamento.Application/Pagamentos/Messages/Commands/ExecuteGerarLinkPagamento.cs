namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;

public sealed record ExecuteGerarLinkPagamento(
    Guid PagamentoId,
    Guid OrdemDeServicoId,
    Guid ClienteId,
    string EmailCliente,
    string NomeCliente,
    decimal Valor,
    DateTime? Vencimento) : IPagamentoExecuteCommand;
