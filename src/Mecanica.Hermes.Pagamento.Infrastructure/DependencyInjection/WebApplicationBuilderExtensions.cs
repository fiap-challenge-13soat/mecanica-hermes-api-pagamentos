#pragma warning disable CA1305 // Serilog Console sink does not expose an IFormatProvider overload
using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Infrastructure.Logging;
using Mecanica.Hermes.Pagamento.Infrastructure.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;

namespace Mecanica.Hermes.Pagamento.Infrastructure.DependencyInjection;

[ExcludeFromCodeCoverage(Justification =
    "Composition root — configuração de Serilog/OTel exercitada pelos tests de integração.")]
public static class WebApplicationBuilderExtensions
{
    private static bool IsHealthCheckLog(LogEvent logEvent)
    {
        if (logEvent.Properties.TryGetValue("RequestPath", out var rp) &&
            rp is ScalarValue { Value: string requestPath } &&
            requestPath.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            return true;

        if (logEvent.Properties.TryGetValue("Path", out var p) &&
            p is ScalarValue { Value: string path } &&
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            return true;

        if (logEvent.Properties.TryGetValue("EndpointName", out var en) &&
            en is ScalarValue { Value: string endpointName } &&
            endpointName.Contains("Health"))
            return true;

        return false;
    }

    public static void AddSerilogConfiguration(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        Log.Information("Application starting up in {EnvironmentName} mode", builder.Environment.EnvironmentName);

        builder.Logging.ClearProviders();

        builder.Host.UseSerilog((ctx, _, lc) =>
        {
            lc
                .ReadFrom.Configuration(ctx.Configuration)
                .Filter.ByExcluding(IsHealthCheckLog)
                .Enrich.WithProperty("ApplicationName", "MecanicaHermes.Pagamento")
                .Enrich.WithSpan()
                .Enrich.With<LogLevelEnricher>();

            if (!EnvironmentDetector.IsObservabilityEnabled)
            {
                lc.WriteTo.Console();
                return;
            }

            var headers = OtlpEnvironment.ResolveLogsHeaders();

            if (headers.Count == 0)
                throw new InvalidOperationException(
                    "OTLP logs authentication is missing. Provide credentials through " +
                    "OTEL_EXPORTER_OTLP_HEADERS (or OTEL_EXPORTER_OTLP_LOGS_HEADERS) " +
                    "when observability is enabled, e.g. \"api-key=<license-key>\".");

            lc.WriteTo.OpenTelemetry(opts =>
            {
                opts.Endpoint = OtlpEnvironment.ResolveLogsEndpoint();
                opts.Protocol = OtlpEnvironment.ResolveLogsProtocol();
                opts.Headers = headers;
                opts.ResourceAttributes = new Dictionary<string, object>
                {
                    ["service.name"] = OtlpEnvironment.ResolveServiceName("mecanica-hermes-pagamento-api"),
                    ["k8s.pod.name"] = EnvironmentDetector.PodName,
                    ["k8s.namespace.name"] = EnvironmentDetector.PodNamespace,
                    ["k8s.node.name"] = EnvironmentDetector.NodeName,
                    ["k8s.cluster.name"] = EnvironmentDetector.ClusterName
                };
            });
        });
    }

    public static void UseSerilogConfiguration(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
    }
}
