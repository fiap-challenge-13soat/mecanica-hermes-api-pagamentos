using MassTransit;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;

[EntityName("pagamento.recusado.v1")]
public sealed record PagamentoRecusadoEvent(
    Guid OrdemDeServicoId,
    Guid PagamentoId,
    string Motivo,
    DateTime RecusadoEm);
