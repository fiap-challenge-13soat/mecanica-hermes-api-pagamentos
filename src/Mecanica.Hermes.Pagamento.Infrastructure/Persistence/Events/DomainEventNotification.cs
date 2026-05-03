using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using MediatR;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Events;

internal sealed record DomainEventNotification<TEvent>(TEvent Event) : INotification
    where TEvent : IDomainEvent;
