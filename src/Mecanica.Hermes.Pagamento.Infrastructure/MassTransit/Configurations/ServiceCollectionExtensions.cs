using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Observability;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using static System.Int32;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Configurations;

[ExcludeFromCodeCoverage(Justification =
    "Composition root — escolhe transporte (AmazonMQ/RabbitMQ/InMemory) e registra saga + consumers.")]
internal static class ServiceCollectionExtensions
{
    public static void ConfigureMassTransit(this IServiceCollection services)
    {
        services.AddMassTransitOptions();

        // DLQ observability: o observer captura `ConsumeFault` (após retries+redelivery esgotados)
        // e emite métrica `messaging.consume_faults` + log estruturado.
        services.AddSingleton<DlqMetrics>();
        services.AddSingleton<MessagingFaultObserver>();

        var transportService = ResolveTransportService();

        switch (transportService)
        {
            case TransportService.RabbitMq:
                services.AddMassTransitWithRabbitMq();
                break;
            case TransportService.AmazonMq:
                services.AddMassTransitWithAmazonMq();
                break;
            default:
                // `None` (ou env inválida em testes unitários): in-memory transport para que a app
                // suba sem dependência externa.
                services.AddMassTransitInMemory();
                break;
        }
    }

    // Default = AmazonMq quando a env var está ausente. Mantém `None` explícito como escape hatch
    // para testes unitários / dev local que não querem broker externo.
    private static TransportService ResolveTransportService()
    {
        var raw = Environment.GetEnvironmentVariable(VariableNames.MassTransitTransportService);
        if (string.IsNullOrWhiteSpace(raw))
            return TransportService.AmazonMq;

        return Enum.TryParse<TransportService>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : TransportService.AmazonMq;
    }

    private static void AddMassTransitWithRabbitMq(this IServiceCollection services)
    {
        var mongoConnectionString = Environment.GetEnvironmentVariable(VariableNames.MongoDbConnectionString);
        var mongoDatabaseName = Environment.GetEnvironmentVariable(VariableNames.MongoDbSagaDatabase)
                                ?? MassTransitOptions.DefaultMongoDbSagaDatabase;

        services.AddMassTransit(x =>
        {
            x.ConfigureHealthCheckOptions(opts =>
            {
                opts.MinimalFailureStatus = HealthStatus.Degraded;
                opts.Tags.Add("ready");
                opts.Tags.Add("messaging");
            });

            x.ConfigureSaga(mongoConnectionString, mongoDatabaseName);
            x.UsingRabbitMq((context, cfg) => cfg.ConfigureRabbitMq(context));
        });
    }

    private static void AddMassTransitWithAmazonMq(this IServiceCollection services)
    {
        var mongoConnectionString = Environment.GetEnvironmentVariable(VariableNames.MongoDbConnectionString);
        var mongoDatabaseName = Environment.GetEnvironmentVariable(VariableNames.MongoDbSagaDatabase)
                                ?? MassTransitOptions.DefaultMongoDbSagaDatabase;

        services.AddMassTransit(x =>
        {
            x.ConfigureHealthCheckOptions(opts =>
            {
                opts.MinimalFailureStatus = HealthStatus.Degraded;
                opts.Tags.Add("ready");
                opts.Tags.Add("messaging");
            });

            x.ConfigureSaga(mongoConnectionString, mongoDatabaseName);
            x.UsingRabbitMq((context, cfg) => cfg.ConfigureAmazonMq(context));
        });
    }

    private static void AddMassTransitInMemory(this IServiceCollection services)
    {
        services.AddMassTransit(x =>
        {
            x.ConfigureSaga(
                mongoConnectionString: null,
                mongoDatabaseName: MassTransitOptions.DefaultMongoDbSagaDatabase);
            x.UsingInMemory((context, cfg) =>
            {
                cfg.UseDelayedMessageScheduler();
                cfg.ConfigureEndpoints(context);
                cfg.ConfigureCommonFactory(context);
            });
        });
    }

    private static void AddMassTransitOptions(this IServiceCollection services)
    {
        _ = TryParse(Environment.GetEnvironmentVariable(VariableNames.MassTransitPrefetchCount), out var prefetchCount);

        var queueNamePrefix = Environment.GetEnvironmentVariable(VariableNames.MassTransitQueueNamePrefix);
        if (string.IsNullOrWhiteSpace(queueNamePrefix))
            queueNamePrefix = MassTransitOptions.DefaultQueueNamePrefix;

        var mongoConnectionString = Environment.GetEnvironmentVariable(VariableNames.MongoDbConnectionString);
        var mongoDatabaseName = Environment.GetEnvironmentVariable(VariableNames.MongoDbSagaDatabase)
                                ?? MassTransitOptions.DefaultMongoDbSagaDatabase;

        services.Configure<MassTransitOptions>(options =>
        {
            options.MassTransitConnectionString =
                Environment.GetEnvironmentVariable(VariableNames.MassTransitConnectionString);
            options.MassTransitPrefetchCount = prefetchCount > 0 ? prefetchCount : 24;
            options.QueueNamePrefix = queueNamePrefix;
            options.MongoDbConnectionString = mongoConnectionString;
            options.MongoDbSagaDatabaseName = mongoDatabaseName;
        });

        services.AddSingleton<IEndpointNameFormatter>(
            new KebabCaseEndpointNameFormatter(queueNamePrefix, false));
    }
}
