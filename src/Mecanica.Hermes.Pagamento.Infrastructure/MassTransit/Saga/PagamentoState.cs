using MassTransit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Saga;

public class PagamentoState : SagaStateMachineInstance, ISagaVersion
{
    public string CurrentState { get; set; } = null!;
    public string? OperacaoEmAndamento { get; set; }
    public DateTime? OperacaoIniciadaEm { get; set; }
    public string? UltimoMotivoFalha { get; set; }
    public Guid? OperationTimeoutTokenId { get; set; }

    /// <summary>
    ///     Versão usada pelo MassTransit.MongoDb para optimistic locking.
    /// </summary>
    public int Version { get; set; }

    public Guid CorrelationId { get; set; }
}
