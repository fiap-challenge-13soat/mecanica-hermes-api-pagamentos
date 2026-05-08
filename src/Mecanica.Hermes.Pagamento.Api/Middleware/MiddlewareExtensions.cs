using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;

namespace Mecanica.Hermes.Pagamento.Api.Middleware;

[ExcludeFromCodeCoverage(Justification = "Pipeline registration helpers — testáveis via WebApplicationFactory em integração.")]
public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
