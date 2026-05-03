namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;

public sealed record ExecuteExpirarPagamento(Guid PagamentoId) : IPagamentoExecuteCommand;
