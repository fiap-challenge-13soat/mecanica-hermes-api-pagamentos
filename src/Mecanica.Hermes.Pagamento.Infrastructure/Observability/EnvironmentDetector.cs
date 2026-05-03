using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Observability;

[ExcludeFromCodeCoverage(Justification = "Apenas leitura de variáveis de ambiente do K8s/host — exercitado em runtime.")]
internal static class EnvironmentDetector
{
    public static bool IsObservabilityEnabled =>
        bool.TryParse(Environment.GetEnvironmentVariable(VariableNames.ObservabilityEnabled), out var enabled) &&
        enabled;

    public static string PodName =>
        Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName;

    public static string PodNamespace =>
        Environment.GetEnvironmentVariable("POD_NAMESPACE") ?? "default";

    public static string ClusterName =>
        Environment.GetEnvironmentVariable("CLUSTER_NAME") ?? "local";
}
