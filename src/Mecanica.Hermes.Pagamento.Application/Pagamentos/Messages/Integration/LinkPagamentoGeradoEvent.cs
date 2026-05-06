using MassTransit;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;

[EntityName("pagamento.link-gerado.v1")]
public sealed record LinkPagamentoGeradoEvent(Guid PagamentoId);
