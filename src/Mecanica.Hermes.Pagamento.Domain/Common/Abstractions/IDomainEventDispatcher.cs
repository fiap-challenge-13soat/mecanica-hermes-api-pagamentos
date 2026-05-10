using Mecanica.Hermes.Shared.Core.Domain;

namespace Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct);
}
