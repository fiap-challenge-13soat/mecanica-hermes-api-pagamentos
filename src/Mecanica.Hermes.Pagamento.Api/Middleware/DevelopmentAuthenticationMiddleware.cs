using System.Security.Claims;
using Mecanica.Hermes.Pagamento.Domain.Common;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Mecanica.Hermes.Pagamento.Api.Middleware;

public class DevelopmentAuthenticationMiddleware
{
    private readonly IHostEnvironment _environment;
    private readonly RequestDelegate _next;

    public DevelopmentAuthenticationMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() && environment.EnvironmentName != EnvironmentNames.Testing)
            throw new InvalidOperationException(
                $"DevelopmentAuthenticationMiddleware foi instanciado em ambiente '{environment.EnvironmentName}'. " +
                "Este middleware concede acesso irrestrito e NUNCA deve ser usado em produção.");

        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if ((_environment.IsDevelopment() || _environment.EnvironmentName == EnvironmentNames.Testing) &&
            context.User.Identity?.IsAuthenticated != true)
        {
            const string FallbackAdminScope = "mechermes-admin/admin";
            const string FallbackClienteScope = "mechermes-admin/client";

            var adminScope = string.IsNullOrWhiteSpace(AuthPolicies.OnlyAdminScope)
                ? FallbackAdminScope
                : AuthPolicies.OnlyAdminScope;
            var clienteScope = string.IsNullOrWhiteSpace(AuthPolicies.AllowClienteScope)
                ? FallbackClienteScope
                : AuthPolicies.AllowClienteScope;

            var scopeValue = string.Join(' ', adminScope, clienteScope);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "test-user-id"),
                new(ClaimTypes.Name, "Test User"),
                new(ClaimTypes.Email, "test@example.com"),
                new("scope", scopeValue)
            };

            var identity = new ClaimsIdentity(claims, "TestAuthentication");
            context.User = new ClaimsPrincipal(identity);
        }

        await _next(context);
    }
}

public static class DevelopmentAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseDevelopmentAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<DevelopmentAuthenticationMiddleware>();
    }
}
