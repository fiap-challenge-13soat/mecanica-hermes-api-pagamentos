using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Shared.Core.Domain;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Common.Abstractions;

public class BaseDomainTests
{
    private sealed class FakeEntity : BaseDomain
    {
        public FakeEntity() { }
        public FakeEntity(Guid id) : base(id) { }
        public void TouchPublic() => MarkAsUpdated();
        public void RestaurarPublic(Guid id, DateTime createdAt, DateTime? updatedAt)
            => RestaurarBase(id, createdAt, updatedAt);
    }

    [Fact]
    public void Construtor_Default_GeraIdVazio()
    {
        var entity = new FakeEntity();

        entity.Id.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ConstrutorComId_AtribuiId()
    {
        var id = Guid.NewGuid();

        var entity = new FakeEntity(id);

        entity.Id.Should().Be(id);
    }

    [Fact]
    public void CreatedAt_DefaultEhUtcAgora()
    {
        var antes = DateTime.UtcNow;
        var entity = new FakeEntity();
        var depois = DateTime.UtcNow;

        entity.CreatedAt.Should().BeOnOrAfter(antes).And.BeOnOrBefore(depois);
    }

    [Fact]
    public void UpdatedAt_DefaultEhNulo()
    {
        var entity = new FakeEntity();

        entity.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsUpdated_DefiniUpdatedAtComoAgora()
    {
        var entity = new FakeEntity();
        var antes = DateTime.UtcNow;

        entity.TouchPublic();

        entity.UpdatedAt.Should().NotBeNull();
        entity.UpdatedAt.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public void RestaurarBase_AtualizaIdCreatedAtEUpdatedAt()
    {
        var entity = new FakeEntity();
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        entity.RestaurarPublic(id, createdAt, updatedAt);

        entity.Id.Should().Be(id);
        entity.CreatedAt.Should().Be(createdAt);
        entity.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void RestaurarBase_AceitaUpdatedAtNulo()
    {
        var entity = new FakeEntity();

        entity.RestaurarPublic(Guid.NewGuid(), DateTime.UtcNow, null);

        entity.UpdatedAt.Should().BeNull();
    }
}
