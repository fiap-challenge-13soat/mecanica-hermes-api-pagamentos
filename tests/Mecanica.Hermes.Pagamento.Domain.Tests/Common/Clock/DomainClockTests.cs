using FluentAssertions;
using Mecanica.Hermes.Pagamento.Domain.Common.Clock;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Domain.Tests.Common.Clock;

public class DomainClockTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    [Fact]
    public void UtcNow_SemOverride_RetornaTempoProximoDoSistema()
    {
        var antes = DateTime.UtcNow;
        var agora = DomainClock.UtcNow;
        var depois = DateTime.UtcNow;

        agora.Should().BeOnOrAfter(antes).And.BeOnOrBefore(depois);
    }

    [Fact]
    public void Override_SubstituiTempoNoEscopo()
    {
        var fixo = new DateTimeOffset(2026, 5, 3, 12, 0, 0, TimeSpan.Zero);
        var fakeProvider = new FixedTimeProvider(fixo);

        using (DomainClock.Override(fakeProvider))
        {
            DomainClock.UtcNow.Should().Be(fixo.UtcDateTime);
        }
    }

    [Fact]
    public void Override_AoFinalizarEscopo_VoltaAoSistemaPadrao()
    {
        var fixo = new DateTimeOffset(2026, 5, 3, 12, 0, 0, TimeSpan.Zero);
        var fakeProvider = new FixedTimeProvider(fixo);

        using (DomainClock.Override(fakeProvider))
        {
            DomainClock.UtcNow.Should().Be(fixo.UtcDateTime);
        }

        // Após o dispose, deve voltar a usar o tempo do sistema
        DomainClock.UtcNow.Should().NotBe(fixo.UtcDateTime);
    }

    [Fact]
    public void Override_RetornaDisposable()
    {
        var fakeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);

        var disposable = DomainClock.Override(fakeProvider);

        disposable.Should().NotBeNull();
        disposable.Dispose();
    }

    [Fact]
    public void Override_ApanhaTempoAtualizadoAposAvanco()
    {
        var inicial = new DateTimeOffset(2026, 5, 3, 12, 0, 0, TimeSpan.Zero);
        var fakeProvider = new FixedTimeProvider(inicial);

        using (DomainClock.Override(fakeProvider))
        {
            fakeProvider.Advance(TimeSpan.FromHours(1));

            DomainClock.UtcNow.Should().Be(inicial.UtcDateTime.AddHours(1));
        }
    }
}
