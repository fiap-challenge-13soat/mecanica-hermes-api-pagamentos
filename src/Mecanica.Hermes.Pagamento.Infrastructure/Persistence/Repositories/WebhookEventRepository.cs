using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Entities;
using MongoDB.Driver;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repositório Mongo (idempotência de webhook) — coberto por testes de integração.")]
internal sealed class WebhookEventRepository(MongoDbContext db, TimeProvider timeProvider)
    : IWebhookEventRepository
{
    public async Task<bool> TryRegisterAsync(
        string mercadoPagoEventId,
        Guid pagamentoId,
        string status,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.WebhookEvents.InsertOneAsync(
                new WebhookEventDocument
                {
                    MercadoPagoEventId = mercadoPagoEventId,
                    PagamentoId = pagamentoId,
                    Status = status,
                    ReceivedAt = timeProvider.GetUtcNow().UtcDateTime
                },
                cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
