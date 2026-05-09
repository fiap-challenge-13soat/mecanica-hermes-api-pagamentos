using System.Globalization;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MercadoPago;

/// <summary>
///     Implementação concreta de <see cref="IPagamentoSchedulerOptions" /> que lê das env vars
///     <c>PAGAMENTO__*</c> com fallbacks razoáveis.
/// </summary>
internal sealed class PagamentoSchedulerOptions : IPagamentoSchedulerOptions
{
    public TimeSpan PollingInterval { get; }
    public int PollingMaxAttempts { get; }
    public TimeSpan OperationTimeout { get; }
    public TimeSpan LinkValidade { get; }

    public PagamentoSchedulerOptions()
    {
        PollingInterval = ParseTimeSpan(VariableNames.PagamentoPollingInterval, TimeSpan.FromMinutes(2));
        PollingMaxAttempts = ParseInt(VariableNames.PagamentoPollingMaxAttempts, 15);
        OperationTimeout = ParseTimeSpan(VariableNames.PagamentoOperationTimeout, TimeSpan.FromMinutes(5));
        LinkValidade = ParseTimeSpan(VariableNames.PagamentoLinkValidade, TimeSpan.FromHours(24));
    }

    private static TimeSpan ParseTimeSpan(string variable, TimeSpan fallback)
    {
        return TimeSpan.TryParse(
            Environment.GetEnvironmentVariable(variable),
            CultureInfo.InvariantCulture,
            out var v) ? v : fallback;
    }

    private static int ParseInt(string variable, int fallback)
    {
        return int.TryParse(
            Environment.GetEnvironmentVariable(variable),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var v) ? v : fallback;
    }
}
