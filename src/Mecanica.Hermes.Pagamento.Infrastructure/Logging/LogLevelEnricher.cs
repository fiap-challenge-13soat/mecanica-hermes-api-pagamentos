using Serilog.Core;
using Serilog.Events;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Logging;

/// <summary>
///     Adiciona ao log o severity numérico do nível Serilog (compatível com OTLP). Permite
///     que dashboards filtrem por número (0=Verbose, 1=Debug, ..., 5=Fatal) sem depender da
///     string em vários idiomas.
/// </summary>
internal sealed class LogLevelEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("LogLevelNumber", (int)logEvent.Level));
    }
}
