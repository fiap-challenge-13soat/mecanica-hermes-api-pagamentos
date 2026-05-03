using System.Diagnostics;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Observability;

internal static class ActivitySources
{
    public const string ApiName = "MecanicaHermes.Pagamento.Api";

    public static readonly ActivitySource Api = new(ApiName, "1.0");
}
