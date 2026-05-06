using MassTransit;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Requests;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

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
        if (existente is not null) return;

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
}
