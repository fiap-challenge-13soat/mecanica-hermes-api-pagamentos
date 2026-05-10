using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence;

/// <summary>
///     Wrapper fino sobre <see cref="IMongoClient" /> com acesso tipado às coleções usadas pela
///     API. <see cref="StartSessionAsync" /> está disponível para callers que precisem iniciar
///     sessões/transactions Mongo (replica set), mas o <c>MongoUnitOfWork</c> atual não as
///     consome — agregado e outbox são escritos sem compartilhar sessão. A reconciliação de
///     consistência fica a cargo do outbox processor (at-least-once).
/// </summary>
[ExcludeFromCodeCoverage(Justification =
    "Wrapper de IMongoClient — coberto por testes de integração com Testcontainers.")]
internal sealed class MongoDbContext
{
    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoClient client, IOptions<MongoOptions> options)
    {
        _client = client;
        _database = client.GetDatabase(options.Value.DatabaseName);
    }

    public IMongoCollection<PagamentoDocument> Pagamentos =>
        _database.GetCollection<PagamentoDocument>(MongoOptions.PagamentosCollection);

    public IMongoCollection<OutboxDocument> Outbox =>
        _database.GetCollection<OutboxDocument>(MongoOptions.OutboxCollection);

    public IMongoCollection<WebhookEventDocument> WebhookEvents =>
        _database.GetCollection<WebhookEventDocument>(MongoOptions.WebhookEventsCollection);

    public Task<IClientSessionHandle> StartSessionAsync(CancellationToken ct = default)
    {
        return _client.StartSessionAsync(cancellationToken: ct);
    }

    public IMongoDatabase Database => _database;
}
