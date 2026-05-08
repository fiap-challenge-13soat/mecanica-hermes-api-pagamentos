using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;
using Mecanica.Hermes.Shared.Application.Messaging;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Commands.VerificarStatus;

/// <summary>
///     Solicita verificação imediata do status no Mercado Pago. Endpoint operacional
///     (debug/forçar reconciliação). Idempotente — apenas dispara um polling adicional.
/// </summary>
public sealed record VerificarStatusPagamentoCommand(Guid PagamentoId)
    : IAsyncCommand, IPagamentoCommand
{
    public object ToMessage()
    {
        return new VerificarStatusPagamentoRequest(PagamentoId);
    }
}
