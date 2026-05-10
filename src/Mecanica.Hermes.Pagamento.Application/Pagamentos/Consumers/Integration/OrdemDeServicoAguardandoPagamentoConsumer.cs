using MassTransit;
using Mecanica.Hermes.Contracts.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Consumers.Integration;

public sealed class OrdemDeServicoAguardandoPagamentoConsumer(
    IPagamentoRepository repository,
    IClienteApiClient clienteApiClient)
    : IConsumer<OrdemDeServicoAguardandoPagamentoEvent>
{
    public async Task Consume(ConsumeContext<OrdemDeServicoAguardandoPagamentoEvent> context)
    {
        var msg = context.Message;

        var existente = await repository.GetByOrdemDeServicoIdAsync(msg.OrdemDeServicoId);

        // Idempotencia parcial: pula apenas se o pagamento atual ainda esta em ciclo de vida
        // ativo (Pendente, LinkGerado, AguardandoConfirmacao, Pago). Quando o pagamento anterior
        // terminou em Recusado/Expirado, e a OS volta para AguardandoPagamento (operador
        // re-iniciou apos reverter), criamos um novo pagamento.
        if (existente is not null && IsAtivoOuPago(existente.StatusAtual.StatusAtual))
            return;

        var cliente = await clienteApiClient.GetByIdAsync(msg.ClienteId, context.CancellationToken);
        if (cliente is null) return;

        await context.Publish(new GerarPagamentoRequest(
            Guid.NewGuid(),
            msg.OrdemDeServicoId,
            msg.ClienteId,
            cliente.Email,
            cliente.NomeExibicao,
            msg.Valor,
            Vencimento: null));
    }

    private static bool IsAtivoOuPago(PagamentoStatus status) =>
        status is PagamentoStatus.PendenteGeracao
            or PagamentoStatus.LinkGerado
            or PagamentoStatus.AguardandoConfirmacao
            or PagamentoStatus.Pago;
}
