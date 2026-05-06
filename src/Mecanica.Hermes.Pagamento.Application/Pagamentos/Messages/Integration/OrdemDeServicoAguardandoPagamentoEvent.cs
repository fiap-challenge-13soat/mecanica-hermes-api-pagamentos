using MassTransit;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;

[EntityName("ordem-de-servico.aguardando-pagamento.v1")]
public sealed record OrdemDeServicoAguardandoPagamentoEvent(
    Guid OrdemDeServicoId,
    Guid ClienteId,
    decimal Valor);
