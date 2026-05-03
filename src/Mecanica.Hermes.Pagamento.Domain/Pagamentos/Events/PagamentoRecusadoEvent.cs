using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Events;

[DomainEventName("pagamento.recusado.v1")]
public record PagamentoRecusadoEvent(
    Guid PagamentoId,
    Guid OrdemDeServicoId,
    string Motivo) : IDomainEvent;
