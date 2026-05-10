using FluentAssertions;
using Mecanica.Hermes.Pagamento.Infrastructure.MercadoPago;
using Xunit;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Tests.MercadoPago;

[Collection("EnvironmentVariableSerial")]
public class PagamentoSchedulerOptionsTests
{
    private const string PollingIntervalVar = "PAGAMENTO__POLLING_INTERVAL";
    private const string PollingMaxAttemptsVar = "PAGAMENTO__POLLING_MAX_ATTEMPTS";
    private const string OperationTimeoutVar = "PAGAMENTO__OPERATION_TIMEOUT";
    private const string LinkValidadeVar = "PAGAMENTO__LINK_VALIDADE";

    private static void ClearAll()
    {
        Environment.SetEnvironmentVariable(PollingIntervalVar, null);
        Environment.SetEnvironmentVariable(PollingMaxAttemptsVar, null);
        Environment.SetEnvironmentVariable(OperationTimeoutVar, null);
        Environment.SetEnvironmentVariable(LinkValidadeVar, null);
    }

    [Fact]
    public void Constructor_NoEnvVars_UsesDefaults()
    {
        ClearAll();
        try
        {
            var opts = new PagamentoSchedulerOptions();

            opts.PollingInterval.Should().Be(TimeSpan.FromMinutes(2));
            opts.PollingMaxAttempts.Should().Be(15);
            opts.OperationTimeout.Should().Be(TimeSpan.FromMinutes(5));
            opts.LinkValidade.Should().Be(TimeSpan.FromHours(24));
        }
        finally
        {
            ClearAll();
        }
    }

    [Fact]
    public void Constructor_WithEnvVarsSet_ParsesValues()
    {
        ClearAll();
        try
        {
            Environment.SetEnvironmentVariable(PollingIntervalVar, "00:00:30");
            Environment.SetEnvironmentVariable(PollingMaxAttemptsVar, "42");
            Environment.SetEnvironmentVariable(OperationTimeoutVar, "00:01:30");
            Environment.SetEnvironmentVariable(LinkValidadeVar, "12:00:00");

            var opts = new PagamentoSchedulerOptions();

            opts.PollingInterval.Should().Be(TimeSpan.FromSeconds(30));
            opts.PollingMaxAttempts.Should().Be(42);
            opts.OperationTimeout.Should().Be(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(30)));
            opts.LinkValidade.Should().Be(TimeSpan.FromHours(12));
        }
        finally
        {
            ClearAll();
        }
    }

    [Fact]
    public void Constructor_WithInvalidEnvVars_FallsBackToDefaults()
    {
        ClearAll();
        try
        {
            Environment.SetEnvironmentVariable(PollingIntervalVar, "invalid");
            Environment.SetEnvironmentVariable(PollingMaxAttemptsVar, "not-a-number");

            var opts = new PagamentoSchedulerOptions();

            opts.PollingInterval.Should().Be(TimeSpan.FromMinutes(2));
            opts.PollingMaxAttempts.Should().Be(15);
        }
        finally
        {
            ClearAll();
        }
    }
}

[CollectionDefinition("EnvironmentVariableSerial", DisableParallelization = true)]
#pragma warning disable CA1711
public sealed class EnvironmentVariableSerialCollection
{
}
#pragma warning restore CA1711
