namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;

public sealed record ExecuteVerificarStatusPagamento(Guid PagamentoId) : IPagamentoExecuteCommand;
