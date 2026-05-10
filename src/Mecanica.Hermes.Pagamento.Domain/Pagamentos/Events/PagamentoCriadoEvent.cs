using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Shared.Core.Domain;

namespace Mecanica.Hermes.Pagamento.Domain.Pagamentos.Events;

[DomainEventName("pagamento.criado.v1")]
public record PagamentoCriadoEvent(
    Guid PagamentoId,
    Guid OrdemDeServicoId,
    Guid ClienteId,
    string EmailCliente,
    decimal Valor,
    string Moeda) : IDomainEvent;
