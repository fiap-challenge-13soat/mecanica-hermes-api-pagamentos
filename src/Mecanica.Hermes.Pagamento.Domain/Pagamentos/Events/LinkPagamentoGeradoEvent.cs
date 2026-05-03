using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Events;

[DomainEventName("pagamento.link-gerado.v1")]
public record LinkPagamentoGeradoEvent(
    Guid PagamentoId,
    Guid OrdemDeServicoId,
    string MercadoPagoPreferenceId,
    string Url) : IDomainEvent;
