using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Outbox;
using Mecanica.Hermes.Shared.Observability;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Observability;

[ExcludeFromCodeCoverage(Justification = "Composição de OpenTelemetry — exercitado pelos testes de integração.")]
internal static class ObservabilityConfiguration
{
    public static void AddObservability(this IServiceCollection services)
    {
        if (!EnvironmentDetector.IsObservabilityEnabled)
            return;

        services.AddOpenTelemetry()
            .UseOtlpExporter()
            .ConfigureResource(resource =>
            {
                resource
                    .AddService(OtlpEnvironment.ResolveServiceName("mecanica-hermes-pagamento-api"),
                        serviceVersion: "1.0.0")
                    .AddContainerDetector()
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["k8s.pod.name"] = EnvironmentDetector.PodName,
                        ["k8s.namespace.name"] = EnvironmentDetector.PodNamespace,
                        ["k8s.node.name"] = EnvironmentDetector.NodeName,
                        ["k8s.cluster.name"] = EnvironmentDetector.ClusterName
                    });
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ActivitySources.ApiName)
                    .AddSource("MecanicaHermes.Pagamento.Application")
                    .AddSource("MassTransit")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(PagamentoMetrics.MeterName)
                    .AddMeter(OutboxHealthMetrics.MeterName)
                    .AddMeter("MassTransit")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            });
    }
}
