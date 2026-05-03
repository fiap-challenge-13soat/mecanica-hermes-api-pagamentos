namespace Mecanica.Hermes.Pagamento.Domain.Common.Clock;

/// <summary>
///     Provedor de tempo para o domínio. Usa <see cref="TimeProvider.System" /> por padrão
///     e pode ser substituído em testes via <see cref="Override" /> para obter tempo determinístico.
///     Padrão Ambient Context — evita propagar TimeProvider por todos os construtores do domínio
///     enquanto permite testar comportamentos que dependem de tempo (vencimento, polling, timestamps).
/// </summary>
public static class DomainClock
{
    private static readonly AsyncLocal<TimeProvider?> _override = new();

    /// <summary>
    ///     Retorna UTC now do provider ativo (injetado em testes ou sistema em produção).
    /// </summary>
    public static DateTime UtcNow => (_override.Value ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    /// <summary>
    ///     Substitui o provider de tempo para o contexto de execução atual (scoped per-task).
    ///     Retorna um disposable que restaura o provider ao ser descartado.
    ///     Use em testes com <c>using var _ = DomainClock.Override(fakeTimeProvider)</c>.
    /// </summary>
    public static IDisposable Override(TimeProvider provider)
    {
        _override.Value = provider;
        return new ClockScope(() => _override.Value = null);
    }

    private sealed class ClockScope(Action restore) : IDisposable
    {
        public void Dispose()
        {
            restore();
        }
    }
}
