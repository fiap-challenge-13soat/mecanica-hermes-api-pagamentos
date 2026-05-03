namespace Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;

/// <summary>
///     Configurações de tempo (polling, vencimento) usadas pelos consumers e SAGA.
///     Implementação fica em Infrastructure (lê das env vars <c>PAGAMENTO__*</c>).
/// </summary>
public interface IPagamentoSchedulerOptions
{
    /// <summary>Intervalo entre verificações de status no Mercado Pago.</summary>
    TimeSpan PollingInterval { get; }

    /// <summary>Limite máximo de verificações de polling antes de marcar o pagamento como expirado.</summary>
    int PollingMaxAttempts { get; }

    /// <summary>Prazo máximo de uma operação SAGA antes de ser abortada por timeout.</summary>
    TimeSpan OperationTimeout { get; }

    /// <summary>Validade default do link de pagamento (vencimento da preference).</summary>
    TimeSpan LinkValidade { get; }
}
