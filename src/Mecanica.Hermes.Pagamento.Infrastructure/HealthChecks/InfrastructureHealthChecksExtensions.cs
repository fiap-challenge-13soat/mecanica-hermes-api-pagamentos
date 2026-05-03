using System.Diagnostics.CodeAnalysis;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Mecanica.Hermes.Pagamento.Infrastructure.HealthChecks;

[ExcludeFromCodeCoverage(Justification = "Composition root — registra health checks de Mongo.")]
public static class InfrastructureHealthChecksExtensions
{
    /// <summary>
    ///     Registra os health checks de infraestrutura: MongoDB (database principal) +
    ///     MongoDB SAGA (somente conexão de pong). Use as tags <c>"ready"</c> e
    ///     <c>"database"</c> para incluir em <c>/healthz/ready</c>.
    /// </summary>
    public static IHealthChecksBuilder AddInfrastructureHealthChecks(
        this IHealthChecksBuilder builder,
        params string[] tags)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(VariableNames.MongoDbConnectionString)
            ?? "mongodb://localhost:27017";

        builder.AddMongoDb(
            sp => MongoDbDriverFactory.Create(connectionString),
            name: "mongodb",
            failureStatus: HealthStatus.Unhealthy,
            tags: tags);

        return builder;
    }

    private static class MongoDbDriverFactory
    {
        public static MongoDB.Driver.IMongoClient Create(string connectionString)
        {
            return new MongoDB.Driver.MongoClient(connectionString);
        }
    }
}
