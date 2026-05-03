using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Events;

[DomainEventName("pagamento.confirmado.v1")]
public record PagamentoConfirmadoEvent(
    Guid PagamentoId,
    Guid OrdemDeServicoId,
    PagamentoConfirmadoVia Via) : IDomainEvent;
