using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain.Pagamentos.Enums;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Mappings;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.UnitOfWork;
using Mecanica.Hermes.Shared.Core.Pagination;
using Mecanica.Hermes.Shared.Core.Persistence;
using MongoDB.Driver;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repositório Mongo — coberto por testes de integração com Testcontainers.")]
internal sealed class PagamentoRepository(MongoDbContext db, IMongoUnitOfWork uow) : IPagamentoRepository
{
    public async Task<Domain.Pagamentos.Pagamento?> GetByIdAsync(Guid id)
    {
        // Reads não participam da transação — não há needs de isolation snapshot aqui.
        var doc = await db.Pagamentos
            .Find(d => d.Id == id)
            .FirstOrDefaultAsync();

        return doc?.ToDomain();
    }

    public async Task<Domain.Pagamentos.Pagamento?> GetByOrdemDeServicoIdAsync(Guid ordemDeServicoId)
    {
        var doc = await db.Pagamentos
            .Find(d => d.OrdemDeServicoId == ordemDeServicoId)
            .FirstOrDefaultAsync();

        return doc?.ToDomain();
    }

    public async Task<PagedResult<Domain.Pagamentos.Pagamento>> ListByStatusAsync(
        PagamentoStatus status,
        PaginationParams paginationParams)
    {
        var statusName = status.ToString();
        var filter = Builders<PagamentoDocument>.Filter.Eq(d => d.StatusAtual, statusName);

        var totalCount = await db.Pagamentos.CountDocumentsAsync(filter);

        var docs = await db.Pagamentos
            .Find(filter)
            .SortBy(d => d.CreatedAt)
            .Skip(paginationParams.Skip)
            .Limit(paginationParams.PageSize)
            .ToListAsync();

        return PagedResult<Domain.Pagamentos.Pagamento>.Create(
            docs.Select(d => d.ToDomain()).ToList(),
            paginationParams.Page,
            paginationParams.PageSize,
            (int)totalCount);
    }

    public async Task<PagedResult<Domain.Pagamentos.Pagamento>> ListAllAsync(PaginationParams paginationParams)
    {
        var totalCount = await db.Pagamentos.CountDocumentsAsync(FilterDefinition<PagamentoDocument>.Empty);

        var docs = await db.Pagamentos
            .Find(FilterDefinition<PagamentoDocument>.Empty)
            .SortBy(d => d.CreatedAt)
            .Skip(paginationParams.Skip)
            .Limit(paginationParams.PageSize)
            .ToListAsync();

        return PagedResult<Domain.Pagamentos.Pagamento>.Create(
            docs.Select(d => d.ToDomain()).ToList(),
            paginationParams.Page,
            paginationParams.PageSize,
            (int)totalCount);
    }

    public async Task AddAsync(Domain.Pagamentos.Pagamento pagamento)
    {
        // Sessão é compartilhada com o UoW — Insert do agregado e Insert dos outbox docs
        // serão comitados juntos (ou abortados juntos) na transação Mongo.
        var session = await uow.GetOrStartSessionAsync(CancellationToken.None);
        var document = pagamento.ToDocument(version: 1);
        await db.Pagamentos.InsertOneAsync(session, document);
        uow.CollectEventsFrom(pagamento);
    }

    public async Task UpdateAsync(Domain.Pagamentos.Pagamento pagamento)
    {
        var session = await uow.GetOrStartSessionAsync(CancellationToken.None);

        var existing = await db.Pagamentos
            .Find(session, d => d.Id == pagamento.Id)
            .Project(d => new { d.Version })
            .FirstOrDefaultAsync();

        var currentVersion = existing?.Version ?? 0;
        var nextVersion = currentVersion + 1;

        var document = pagamento.ToDocument(nextVersion);

        // Optimistic concurrency: filtro inclui a versão esperada; se outro processo atualizou,
        // o ReplaceOne afeta 0 documentos e nós lançamos ConcurrencyConflictException.
        var filter = Builders<PagamentoDocument>.Filter.And(
            Builders<PagamentoDocument>.Filter.Eq(d => d.Id, pagamento.Id),
            Builders<PagamentoDocument>.Filter.Eq(d => d.Version, currentVersion));

        var result = await db.Pagamentos.ReplaceOneAsync(
            session,
            filter,
            document,
            new ReplaceOptions { IsUpsert = false });

        if (result.MatchedCount == 0)
            throw new ConcurrencyConflictException(
                $"Pagamento {pagamento.Id} foi modificado concorrentemente (versão esperada {currentVersion}).");

        uow.CollectEventsFrom(pagamento);
    }
}
