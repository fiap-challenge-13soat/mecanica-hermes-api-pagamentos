using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mecanica.Hermes.Pagamento.Application;
using Mecanica.Hermes.Pagamento.Application.Common.Behaviors;
using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Ports;
using Mecanica.Hermes.Pagamento.Domain;
using Mecanica.Hermes.Pagamento.Infrastructure.HttpClients;
using Mecanica.Hermes.Pagamento.Infrastructure.HttpClients.M2M;
using Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Adapters;
using Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Configurations;
using Mecanica.Hermes.Pagamento.Infrastructure.MercadoPago;
using Mecanica.Hermes.Pagamento.Infrastructure.Observability;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.DependencyInjection;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Repositories;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;
using Mecanica.Hermes.Shared.Application.Behaviors;
using Mecanica.Hermes.Shared.Application.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;

namespace Mecanica.Hermes.Pagamento.Infrastructure.DependencyInjection;

[ExcludeFromCodeCoverage(Justification = "Composition root — coberto por testes de integração.")]
public static class ServiceCollectionExtensions
{
    // Default público da API do Mercado Pago. Sobrescrito por MERCADO_PAGO__API_BASE_URL
    // (necessário para apontar ao WireMock nos testes E2E).
    private const string DefaultMercadoPagoApiBaseUrl = "https://api.mercadopago.com/";

    // Separador de segmento de URI — exigido pelo HttpClient.BaseAddress como
    // sufixo, ou requests relativos (ex.: "checkout/preferences") perdem o último
    // segmento por causa da semântica do RFC 3986 §5.3.
    private const string UriSegmentSeparator = "/";

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
            static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? string.Empty;

            options.AccessToken = Env(VariableNames.MercadoPagoAccessToken);
            options.PublicKey = Env(VariableNames.MercadoPagoPublicKey);
            options.WebhookSecret = Env(VariableNames.MercadoPagoWebhookSecret);
            options.NotificationUrl = Env(VariableNames.MercadoPagoNotificationUrl);
            options.SuccessUrl = Env(VariableNames.MercadoPagoSuccessUrl);
            options.FailureUrl = Env(VariableNames.MercadoPagoFailureUrl);
            options.PendingUrl = Env(VariableNames.MercadoPagoPendingUrl);
            options.ApiBaseUrl = Environment.GetEnvironmentVariable(VariableNames.MercadoPagoApiBaseUrl)
                                 ?? DefaultMercadoPagoApiBaseUrl;
        });
        services.AddSingleton<WebhookSignatureValidator>();
        var mpBaseUrl = Environment.GetEnvironmentVariable(VariableNames.MercadoPagoApiBaseUrl)
                        ?? DefaultMercadoPagoApiBaseUrl;
        services.AddHttpClient<IMercadoPagoClient, MercadoPagoPreferenceClient>(client =>
            {
                var normalizedBase = mpBaseUrl.EndsWith(UriSegmentSeparator, StringComparison.Ordinal)
                    ? mpBaseUrl
                    : mpBaseUrl + UriSegmentSeparator;
                client.BaseAddress = new Uri(normalizedBase, UriKind.Absolute);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.Retry.MaxRetryAttempts = 3;
                options.CircuitBreaker.MinimumThroughput = 10;
            });

        // M2M auth: lê configuração de M2M__* (token endpoint, client id/secret, scope).
        // Quando habilitado, encadeia um DelegatingHandler que injeta `Authorization: Bearer <jwt>`
        // em toda chamada outgoing para Cadastros. Em Development/Testing as env vars ficam
        // vazias e o handler não é encadeado — a Cadastros bypassa via DevAuth nesses cenários.
        services.Configure<M2MTokenProviderOptions>(opt =>
        {
            opt.TokenEndpoint = Environment.GetEnvironmentVariable(VariableNames.M2MTokenEndpoint) ?? string.Empty;
            opt.ClientId = Environment.GetEnvironmentVariable(VariableNames.M2MClientId) ?? string.Empty;
            opt.ClientSecret = Environment.GetEnvironmentVariable(VariableNames.M2MClientSecret) ?? string.Empty;
            opt.Scope = Environment.GetEnvironmentVariable(VariableNames.M2MScope) ?? string.Empty;
        });

        var m2mEnabled = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(VariableNames.M2MTokenEndpoint));

        if (m2mEnabled)
        {
            services.AddSingleton(TimeProvider.System);
            services.AddHttpClient<IM2MTokenProvider, ClientCredentialsTokenProvider>();
            services.AddTransient<M2MAuthorizationDelegatingHandler>();
        }

        var cadastrosBaseUrl = Environment.GetEnvironmentVariable(VariableNames.CadastrosApiBaseUrl)
                               ?? "http://localhost:5000/";
        var cadastrosClientBuilder = services.AddHttpClient<IClienteApiClient, ClienteApiClient>(client =>
            {
                client.BaseAddress = new Uri(cadastrosBaseUrl.TrimEnd('/') + "/");
            });

        // O DelegatingHandler precisa rodar ANTES do StandardResilienceHandler para que
        // 401 cause refresh + retry com token novo (em vez de retry com token expirado).
        if (m2mEnabled)
            cadastrosClientBuilder.AddHttpMessageHandler<M2MAuthorizationDelegatingHandler>();

        cadastrosClientBuilder.AddStandardResilienceHandler(options =>
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
