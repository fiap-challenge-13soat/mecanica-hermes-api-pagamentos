using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Mecanica.Hermes.Pagamento.Api;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Contracts;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Pagamentos.Validators;
using Mecanica.Hermes.Pagamento.Api.Endpoints.Webhooks;
using Mecanica.Hermes.Pagamento.Api.Middleware;
using Mecanica.Hermes.Pagamento.Infrastructure.DependencyInjection;
using Mecanica.Hermes.Pagamento.Infrastructure.HealthChecks;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;
using Mecanica.Hermes.Shared.AspNetCore;
using Mecanica.Hermes.Shared.AspNetCore.Middleware;
using Mecanica.Hermes.Shared.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;
using IPNetwork = System.Net.IPNetwork;

var builder = WebApplication.CreateSlimBuilder(args);

if (builder.Environment.EnvironmentName != EnvironmentNames.Testing)
    builder.AddSerilogConfiguration();

builder.Services.ConfigureInfrastructureServices(typeof(IApiAssemblyMarker).Assembly);

builder.Services.AddSingleton<IValidator<GerarPagamentoRequest>, GerarPagamentoRequestValidator>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("default", policy =>
    {
        if (builder.Environment.IsDevelopment() ||
            builder.Environment.EnvironmentName == EnvironmentNames.Testing)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        var origins = (Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    var knownProxiesRaw = Environment.GetEnvironmentVariable("FORWARDED_HEADERS_KNOWN_PROXIES") ?? string.Empty;
    var knownProxies = knownProxiesRaw
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (builder.Environment.IsDevelopment() ||
        builder.Environment.EnvironmentName == EnvironmentNames.Testing ||
        knownProxies.Length == 0)
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
    else
    {
        foreach (var entry in knownProxies)
            if (entry.Contains('/') &&
                IPNetwork.TryParse(entry, out var network))
                options.KnownIPNetworks.Add(network);
            else if (IPAddress.TryParse(entry, out var ip))
                options.KnownProxies.Add(ip);
    }
});

var isTesting = builder.Environment.EnvironmentName == EnvironmentNames.Testing;
var rlFromEnv = int.TryParse(Environment.GetEnvironmentVariable("RATE_LIMIT_PERMIT_PER_MINUTE"), out var p) ? p : 60;
var rlPermit = isTesting ? int.MaxValue : rlFromEnv;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rlPermit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

builder.Services.AddHealthChecks()
    .AddInfrastructureHealthChecks(HealthCheckTags.Ready, HealthCheckTags.Database, HealthCheckTags.Saga);

builder.Host.ConfigureHostBuilder();

var app = builder.Build();

if (builder.Environment.EnvironmentName != EnvironmentNames.Testing)
    app.UseSerilogConfiguration();

app.UseForwardedHeaders();
app.UseCorrelationId();
app.UseGlobalExceptionHandler();
app.UseCors("default");
app.UseRateLimiter();
app.MapOpenApi("/openapi/pagamentos/v1.json");
app.MapScalarApiReference(options => 
{
    options.WithEndpoint("/openapi/pagamentos/v1.json"); 
});

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == EnvironmentNames.Testing)
{
    app.UseDevelopmentAuthentication(new DevelopmentAuthenticationOptions
    {
        Scopes = [AuthPolicies.OnlyAdminScope, AuthPolicies.AllowClienteScope]
    });
    app.Logger.LogWarning(
        "DevelopmentAuthenticationMiddleware ENABLED — environment: {Environment}. " +
        "All unauthenticated requests will be granted full access. " +
        "This middleware must NEVER run in production.",
        app.Environment.EnvironmentName);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = hc => hc.Tags.Contains(HealthCheckTags.Ready)
});
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = hc => hc.Tags.Contains(HealthCheckTags.Ready)
});

app.MapPagamentosEndpoints();
app.MapMercadoPagoWebhook();

await app.InitializeInfrastructureAsync();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
lifetime.ApplicationStarted.Register(() =>
    logger.LogInformation("API de Pagamentos iniciada com sucesso."));

await app.RunAsync();
