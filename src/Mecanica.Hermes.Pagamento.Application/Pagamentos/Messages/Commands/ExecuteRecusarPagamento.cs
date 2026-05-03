namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;

public sealed record ExecuteRecusarPagamento(Guid PagamentoId, string Motivo)
    : IPagamentoExecuteCommand;
