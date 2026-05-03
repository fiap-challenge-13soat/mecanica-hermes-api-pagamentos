using Mecanica.Hermes.Pagamento.Domain.Common.Results;
using MediatR;

namespace Mecanica.Hermes.Pagamento.Application.Common.Messaging;

/// <summary>
///     Handler genérico que processa qualquer <see cref="IAsyncCommand" />: publica a
///     mensagem retornada por <see cref="IAsyncCommand.ToMessage" /> via
///     <see cref="IAsyncMessagePublisher" /> (port hexagonal) e retorna
///     <see cref="Result.Accepted" /> indicando que o trabalho foi enfileirado.
///     Funciona graças a <c>cfg.RegisterGenericHandlers = true</c> no registro do MediatR.
/// </summary>
public sealed class AsyncCommandHandler<TRequest>(IAsyncMessagePublisher publisher)
    : IRequestHandler<TRequest, Result>
    where TRequest : IAsyncCommand
{
    public async Task<Result> Handle(TRequest request, CancellationToken cancellationToken)
    {
        await publisher.Publish(request.ToMessage(), cancellationToken);
        return Result.Accepted();
    }
}
