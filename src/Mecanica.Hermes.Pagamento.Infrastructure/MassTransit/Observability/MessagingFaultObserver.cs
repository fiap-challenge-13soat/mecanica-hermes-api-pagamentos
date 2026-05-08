using MassTransit;
using Microsoft.Extensions.Logging;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Observability;

/// <summary>
///     Observa o pipeline de consumo MassTransit e emite métrica + log estruturado quando
///     uma mensagem falha em definitivo (após retries e redelivery esgotados).
///     <para>
///         <c>ConsumeFault</c> é disparado pelo MassTransit depois que <c>UseMessageRetry</c> +
///         <c>UseDelayedRedelivery</c> tentaram todas as iterações e a mensagem está prestes a
///         ser publicada na exchange <c>_error</c>. Este é o ponto certo para alarmar — não em
///         retries intermediários.
///     </para>
/// </summary>
internal sealed class MessagingFaultObserver(
    DlqMetrics metrics,
    ILogger<MessagingFaultObserver> logger) : IConsumeObserver
{
    public Task PreConsume<T>(ConsumeContext<T> context) where T : class
    {
        return Task.CompletedTask;
    }

    public Task PostConsume<T>(ConsumeContext<T> context) where T : class
    {
        return Task.CompletedTask;
    }

    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
    {
        var messageType = typeof(T).Name;
        var exceptionType = exception.GetType().Name;

        metrics.ConsumeFault(messageType, exceptionType);

        logger.LogError(exception,
            "Consumer falhou em definitivo para {MessageType} (MessageId={MessageId}). " +
            "Mensagem será movida para _error (DLQ). Exception={ExceptionType}.",
            messageType, context.MessageId, exceptionType);

        return Task.CompletedTask;
    }
}
