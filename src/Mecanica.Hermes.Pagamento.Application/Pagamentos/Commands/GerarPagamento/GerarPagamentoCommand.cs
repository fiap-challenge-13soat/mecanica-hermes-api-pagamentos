using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Shared.Application.Messaging;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Commands.GerarPagamento;

public sealed record GerarPagamentoCommand(
    Guid PagamentoId,
    Guid OrdemDeServicoId,
    Guid ClienteId,
    string EmailCliente,
    string NomeCliente,
    decimal Valor,
    DateTime? Vencimento) : IAsyncCommand, IPagamentoCommand
{
    public object ToMessage()
    {
        return new GerarPagamentoRequest(
            PagamentoId,
            OrdemDeServicoId,
            ClienteId,
            EmailCliente,
            NomeCliente,
            Valor,
            Vencimento);
    }
}
