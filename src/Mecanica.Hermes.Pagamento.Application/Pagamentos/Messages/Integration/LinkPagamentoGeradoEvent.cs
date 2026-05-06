using MassTransit;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;

[EntityName("pagamento.link-gerado")]
public sealed record LinkPagamentoGeradoEvent(Guid PagamentoId);
