using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;

namespace Mecanica.Hermes.Pagamento.Application.Common.Persistence;

public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken cancellationToken);
    void CollectEventsFrom(AggregateRoot aggregate);
}
