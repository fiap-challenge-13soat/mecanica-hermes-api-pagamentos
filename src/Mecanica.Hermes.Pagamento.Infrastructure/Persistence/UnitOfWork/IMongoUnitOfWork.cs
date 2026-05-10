using Mecanica.Hermes.Shared.Core.Persistence;
using MongoDB.Driver;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.UnitOfWork;

/// <summary>
///     Extensão interna do <see cref="IUnitOfWork" /> exposta apenas a repositórios Mongo.
///     Permite que o repositório obtenha (de forma lazy) a sessão Mongo aberta pelo UoW
///     e propague-a para suas operações de escrita — garantindo que <b>agregado + outbox</b>
///     sejam persistidos na mesma transação Mongo (commit atômico via replica set).
/// </summary>
internal interface IMongoUnitOfWork : IUnitOfWork
{
    /// <summary>
    ///     Retorna a sessão Mongo ativa do UoW, criando-a (e iniciando uma transaction)
    ///     na primeira chamada do escopo. Chamadas subsequentes no mesmo escopo retornam
    ///     a mesma sessão e a mesma transação.
    /// </summary>
    Task<IClientSessionHandle> GetOrStartSessionAsync(CancellationToken cancellationToken);
}
