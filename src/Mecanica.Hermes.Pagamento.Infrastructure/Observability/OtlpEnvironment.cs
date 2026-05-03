using Mecanica.Hermes.Pagamento.Infrastructure.Settings;
using Serilog.Sinks.OpenTelemetry;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Observability;

internal static class OtlpEnvironment
{
    public static string ResolveServiceName(string fallback)
    {
        return Environment.GetEnvironmentVariable(VariableNames.OtelServiceName) ?? fallback;
    }

    public static string ResolveLogsEndpoint()
    {
        return Environment.GetEnvironmentVariable(VariableNames.OtelExporterOtlpLogsEndpoint)
               ?? Environment.GetEnvironmentVariable(VariableNames.OtelExporterOtlpEndpoint)
               ?? "http://localhost:4318/v1/logs";
    }

    public static OtlpProtocol ResolveLogsProtocol()
    {
        var raw = Environment.GetEnvironmentVariable(VariableNames.OtelExporterOtlpLogsProtocol)
                  ?? Environment.GetEnvironmentVariable(VariableNames.OtelExporterOtlpProtocol)
                  ?? "http/protobuf";
        return raw.Trim().ToLowerInvariant() switch
        {
            "grpc" => OtlpProtocol.Grpc,
            _ => OtlpProtocol.HttpProtobuf
        };
    }

    public static IDictionary<string, string> ResolveLogsHeaders()
    {
        var raw = Environment.GetEnvironmentVariable(VariableNames.OtelExporterOtlpLogsHeaders)
                  ?? Environment.GetEnvironmentVariable(VariableNames.OtelExporterOtlpHeaders)
                  ?? string.Empty;

        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = entry.IndexOf('=', StringComparison.Ordinal);
            if (idx <= 0) continue;
            var key = entry[..idx].Trim();
            var value = entry[(idx + 1)..].Trim();
            if (key.Length > 0) headers[key] = value;
        }

        return headers;
    }
}
