using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Shared.Core.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Events;

/// <summary>
///     Despacha cada Domain Event como uma <see cref="DomainEventNotification{T}" /> via MediatR.
///     Falhas em handlers são capturadas, logadas e a iteração continua para isolar eventos.
/// </summary>
internal sealed partial class DomainEventDispatcher(
    IPublisher publisher,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var e in events)
            try
            {
                var notificationType = typeof(DomainEventNotification<>).MakeGenericType(e.GetType());
                var notification = (INotification)Activator.CreateInstance(notificationType, e)!;
                await publisher.Publish(notification, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Cancelamento do host/outbox deve abortar o batch — não tratar como falha de handler.
                throw;
            }
            catch (Exception ex)
            {
                // Fronteira de erro intencional: um handler de notificação MediatR quebrado
                // não pode abortar o processamento dos demais eventos do batch (Sonar S2221 dispensado).
                LogDispatchFailure(logger, ex, e.GetType().Name);
            }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Falha ao despachar Domain Event {EventType}. Continuando com os demais.")]
    private static partial void LogDispatchFailure(ILogger logger, Exception exception, string eventType);
}
