using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mecanica.Hermes.Pagamento.Application;
using Mecanica.Hermes.Pagamento.Application.Common.Behaviors;
using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain;
using Mecanica.Hermes.Pagamento.Infrastructure.HttpClients;
using Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Adapters;
using Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Configurations;
using Mecanica.Hermes.Pagamento.Infrastructure.MercadoPago;
using Mecanica.Hermes.Pagamento.Infrastructure.Observability;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.DependencyInjection;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Repositories;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;

namespace Mecanica.Hermes.Pagamento.Infrastructure.DependencyInjection;

[ExcludeFromCodeCoverage(Justification = "Composition root — coberto por testes de integração.")]
public static class ServiceCollectionExtensions
{
    public static void ConfigureInfrastructureServices(this IServiceCollection services, Assembly apiAssemblyMarker)
    {
        services.AddMongoPersistence();
        services.AddObservability();
        services.ConfigureMassTransit();

        services.AddSingleton<PagamentoMetrics>();
        services.AddSingleton<IPagamentoMetrics>(sp => sp.GetRequiredService<PagamentoMetrics>());

        services.AddScoped<IAsyncMessagePublisher, MassTransitMessagePublisher>();

        services.AddSingleton<IPagamentoSchedulerOptions, PagamentoSchedulerOptions>();

        // Ports do webhook + repositórios extras
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();

        // Mercado Pago
        services.Configure<MercadoPagoOptions>(options =>
        {
            options.AccessToken = Environment.GetEnvironmentVariable(VariableNames.MercadoPagoAccessToken) ?? string.Empty;
            options.PublicKey = Environment.GetEnvironmentVariable(VariableNames.MercadoPagoPublicKey) ?? string.Empty;
            options.WebhookSecret = Environment.GetEnvironmentVariable(VariableNames.MercadoPagoWebhookSecret) ?? string.Empty;
            options.NotificationUrl = Environment.GetEnvironmentVariable(VariableNames.MercadoPagoNotificationUrl) ?? string.Empty;
            options.SuccessUrl = Environment.GetEnvironmentVariable(VariableNames.MercadoPagoSuccessUrl) ?? string.Empty;
            options.FailureUrl = Environment.GetEnvironmentVariable(VariableNames.MercadoPagoFailureUrl) ?? string.Empty;
            options.PendingUrl = Environment.GetEnvironmentVariable(VariableNames.MercadoPagoPendingUrl) ?? string.Empty;
            options.ApiBaseUrl = Environment.GetEnvironmentVariable(VariableNames.MercadoPagoApiBaseUrl)
                                 ?? "https://api.mercadopago.com";
        });
        services.AddSingleton<WebhookSignatureValidator>();
        var mpBaseUrl = Environment.GetEnvironmentVariable(VariableNames.MercadoPagoApiBaseUrl)
                        ?? "https://api.mercadopago.com";
        services.AddHttpClient<IMercadoPagoClient, MercadoPagoPreferenceClient>(client =>
            {
                client.BaseAddress = new Uri(mpBaseUrl.TrimEnd('/') + "/");
            })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.Retry.MaxRetryAttempts = 3;
                options.CircuitBreaker.MinimumThroughput = 10;
            });

        var cadastrosBaseUrl = Environment.GetEnvironmentVariable(VariableNames.CadastrosApiBaseUrl)
                               ?? "http://localhost:5000/";
        services.AddHttpClient<IClienteApiClient, ClienteApiClient>(client =>
            {
                client.BaseAddress = new Uri(cadastrosBaseUrl.TrimEnd('/') + "/");
            })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.Retry.MaxRetryAttempts = 2;
                options.CircuitBreaker.MinimumThroughput = 5;
            });

        services.AddMediatR(cfg =>
        {
            cfg.LicenseKey = Environment.GetEnvironmentVariable(VariableNames.MediatRLicenseKey);
            cfg.RegisterServicesFromAssemblies(apiAssemblyMarker);
            cfg.RegisterServicesFromAssemblyContaining<IApplicationAssemblyMarker>();
            cfg.RegisterServicesFromAssemblyContaining<IDomainAssemblyMarker>();
            cfg.RegisterServicesFromAssemblyContaining<IInfrastructureAssemblyMarker>();
            cfg.RegisterGenericHandlers = true;
            cfg.AddOpenBehavior(typeof(TracingBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = Environment.GetEnvironmentVariable(VariableNames.AuthAuthority);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = Environment.GetEnvironmentVariable(VariableNames.AuthIssuer),
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.AllowClienteScope, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                {
                    var scopeClaims = context.User.FindAll("scope");
                    return scopeClaims.Any(scopeClaim =>
                    {
                        var scopes = scopeClaim.Value
                            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        return scopes.Contains(AuthPolicies.AllowClienteScope) ||
                               scopes.Contains(AuthPolicies.OnlyAdminScope);
                    });
                });
            })
            .AddPolicy(AuthPolicies.OnlyAdminScope, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                {
                    var scopeClaims = context.User.FindAll("scope");
                    return scopeClaims.Any(scopeClaim =>
                    {
                        var scopes = scopeClaim.Value
                            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        return scopes.Contains(AuthPolicies.OnlyAdminScope);
                    });
                });
            });
    }
}
