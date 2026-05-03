namespace Mecanica.Hermes.Pagamento.Application.Common.Messaging;

/// <summary>
///     Port (Hexagonal) que abstrai a publicação assíncrona de mensagens em um broker.
///     A implementação atual delega para MassTransit (<c>IPublishEndpoint</c>), mas a Application
///     permanece independente dessa escolha.
/// </summary>
public interface IAsyncMessagePublisher
{
    Task Publish(object message, CancellationToken cancellationToken);
}
