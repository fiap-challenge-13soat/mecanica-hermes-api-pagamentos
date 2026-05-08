using System.Diagnostics.Metrics;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Observability;

/// <summary>
///     Métricas de saúde do pipeline de consumo MassTransit. Emitidas pelo
///     <see cref="MessagingFaultObserver" /> quando uma mensagem falha após esgotar a política
///     de retries (immediate + delayed redelivery) e é movida para a fila <c>_error</c> (DLQ).
///     <list type="bullet">
///         <item>
///             <b>messaging.consume_faults</b> — contador de mensagens enviadas à DLQ. Tags:
///             <c>message_type</c> (nome do record), <c>exception_type</c> (nome da exceção).
///         </item>
///     </list>
///     Use o counter para alarmar quando o volume de DLQ cresce — é o sinal típico de
///     consumer quebrado, schema mismatch ou indisponibilidade prolongada de dependência
///     (ex.: Mercado Pago down, Mongo connection saturada).
/// </summary>
internal sealed class DlqMetrics : IDisposable
{
    public const string MeterName = "MecanicaHermes.Pagamento.Messaging";
    private readonly Counter<long> _consumeFaults;

    private readonly Meter _meter;

    public DlqMetrics()
    {
        _meter = new Meter(MeterName);
        _consumeFaults = _meter.CreateCounter<long>(
            "messaging.consume_faults",
            description: "Mensagens que falharam após retries e foram para o _error queue (DLQ).");
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    public void ConsumeFault(string messageType, string exceptionType)
    {
        _consumeFaults.Add(1,
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("exception_type", exceptionType));
    }
}
