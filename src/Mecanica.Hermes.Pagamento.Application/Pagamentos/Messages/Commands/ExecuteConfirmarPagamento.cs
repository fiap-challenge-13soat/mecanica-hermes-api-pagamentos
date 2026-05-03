using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;

public sealed record ExecuteConfirmarPagamento(
    Guid PagamentoId,
    PagamentoConfirmadoVia Via) : IPagamentoExecuteCommand;
