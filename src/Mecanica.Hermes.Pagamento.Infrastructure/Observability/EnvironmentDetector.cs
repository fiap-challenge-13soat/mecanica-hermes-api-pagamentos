using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Observability;

[ExcludeFromCodeCoverage(Justification =
    "Apenas leitura de variáveis de ambiente do K8s/host — exercitado em runtime.")]
internal static class EnvironmentDetector
{
    private const string Unknown = "unknown";

    public static bool IsKubernetes =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST"));

    public static bool IsObservabilityEnabled =>
        IsKubernetes ||
        string.Equals(
            Environment.GetEnvironmentVariable(VariableNames.ObservabilityEnabled),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static string PodName =>
        Environment.GetEnvironmentVariable("MY_POD_NAME") ?? Unknown;

    public static string PodNamespace =>
        Environment.GetEnvironmentVariable("MY_POD_NAMESPACE") ?? Unknown;

    public static string NodeName =>
        Environment.GetEnvironmentVariable("MY_NODE_NAME") ?? Unknown;

    public static string ClusterName =>
        Environment.GetEnvironmentVariable("NEW_RELIC_METADATA_KUBERNETES_CLUSTER_NAME")
        ?? Environment.GetEnvironmentVariable("MY_CLUSTER_NAME")
        ?? Unknown;
}
