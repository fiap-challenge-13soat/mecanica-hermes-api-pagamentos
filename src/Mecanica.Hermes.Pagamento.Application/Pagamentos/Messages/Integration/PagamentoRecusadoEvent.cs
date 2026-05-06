using MassTransit;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;

[EntityName("pagamento.recusado")]
public sealed record PagamentoRecusadoEvent(
    Guid OrdemDeServicoId,
    Guid PagamentoId,
    string Motivo,
    DateTime RecusadoEm);
