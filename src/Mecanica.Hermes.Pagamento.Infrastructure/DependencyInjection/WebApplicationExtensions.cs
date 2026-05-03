using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;

namespace Mecanica.Hermes.Pagamento.Infrastructure.DependencyInjection;

[ExcludeFromCodeCoverage(Justification = "Hook reservado para inicialização pós-startup; sem lógica testável.")]
public static class WebApplicationExtensions
{
    /// <summary>
    ///     Hook reservado para inicializações pós-startup (futuro: refresh de cache, warmup).
    ///     A criação dos índices Mongo é feita pelo <c>MongoIndexInitializer</c> registrado como
    ///     <see cref="Microsoft.Extensions.Hosting.IHostedService" />, não exigindo chamada manual.
    /// </summary>
    public static Task InitializeInfrastructureAsync(this WebApplication app)
    {
        return Task.CompletedTask;
    }
}
