using MassTransit;

namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Integration;

[EntityName("pagamento.confirmado")]
public sealed record PagamentoConfirmadoEvent(
    Guid OrdemDeServicoId,
    Guid PagamentoId,
    DateTime ConfirmadoEm);
