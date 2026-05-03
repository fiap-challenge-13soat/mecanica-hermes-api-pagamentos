using Mecanica.Hermes.Pagamento.Domain.Common.Results;
using MediatR;

namespace Mecanica.Hermes.Pagamento.Application.Common.Messaging;

/// <summary>
///     Comando assíncrono — representa uma intenção de mudança de estado que deve ser
///     despachada via mensageria (MassTransit). Cada implementação converte-se em uma
///     mensagem (via <see cref="ToMessage" />) que é publicada no broker e processada por
///     um consumer dedicado.
/// </summary>
public interface IAsyncCommand : IRequest<Result>
{
    /// <summary>
    ///     Converte o Command da camada de Application no contrato de mensageria publicado
    ///     no broker. Tipicamente um <c>record</c> da pasta <c>Messages/Requests</c>.
    /// </summary>
    object ToMessage();
}
