using Mecanica.Hermes.Pagamento.Domain.Common.Clock;

namespace Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;

public abstract class BaseDomain
{
    /// <summary>
    ///     Construtor padrão usado pela camada de persistência (Mongo driver) e por entidades cuja
    ///     Id será atribuída via <see cref="RestaurarBase" />.
    /// </summary>
    protected BaseDomain()
    {
    }

    /// <summary>
    ///     Construtor explícito para casos onde o Id é fornecido pela camada chamadora
    ///     (ex.: idempotência por Id externo na criação do Pagamento).
    /// </summary>
    protected BaseDomain(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; private set; }
    public DateTime CreatedAt { get; protected set; } = DomainClock.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    protected internal void MarkAsUpdated()
    {
        UpdatedAt = DomainClock.UtcNow;
    }

    protected internal void RestaurarBase(Guid id, DateTime createdAt, DateTime? updatedAt)
    {
        Id = id;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }
}
