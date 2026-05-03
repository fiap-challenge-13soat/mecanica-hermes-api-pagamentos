using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Events;

[DomainEventName("pagamento.expirado.v1")]
public record PagamentoExpiradoEvent(
    Guid PagamentoId,
    Guid OrdemDeServicoId) : IDomainEvent;
