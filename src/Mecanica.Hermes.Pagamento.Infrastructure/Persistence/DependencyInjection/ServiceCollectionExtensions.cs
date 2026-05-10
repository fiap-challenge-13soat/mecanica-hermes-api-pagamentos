using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Mecanica.Hermes.Pagamento.Domain.Common.Abstractions;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Events;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.Outbox;
using Mecanica.Hermes.Pagamento.Infrastructure.Persistence.UnitOfWork;
using Mecanica.Hermes.Pagamento.Infrastructure.Settings;
using Mecanica.Hermes.Shared.Core.Domain;
using Mecanica.Hermes.Shared.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Persistence.DependencyInjection;

[ExcludeFromCodeCoverage(Justification = "Composition root — registra Mongo + Outbox + UoW.")]
internal static class ServiceCollectionExtensions
{
    private static int _conventionsRegistered;

    internal static void AddMongoPersistence(this IServiceCollection services)
    {
        EnsureConventions();

        services.Configure<MongoOptions>(options =>
        {
            options.ConnectionString =
                Environment.GetEnvironmentVariable(VariableNames.MongoDbConnectionString)
                ?? "mongodb://localhost:27017";
            options.DatabaseName =
                Environment.GetEnvironmentVariable(VariableNames.MongoDbDatabase)
                ?? MongoOptions.DefaultDatabase;
            options.SagaDatabaseName =
                Environment.GetEnvironmentVariable(VariableNames.MongoDbSagaDatabase)
                ?? MongoOptions.DefaultSagaDatabase;
        });

        services.AddSingleton<IMongoClient>(sp =>
        {
            var connectionString =
                Environment.GetEnvironmentVariable(VariableNames.MongoDbConnectionString)
                ?? "mongodb://localhost:27017";
            return new MongoClient(connectionString);
        });

        services.AddSingleton<MongoDbContext>();
        services.AddSingleton(TimeProvider.System);

        // Registramos a implementação concreta como Scoped e expomos via DUAS interfaces
        // (IUnitOfWork público + IMongoUnitOfWork interno) para que repositórios Mongo
        // possam pedir a sessão sem expor detalhe de transporte na port pública.
        services.AddScoped<MongoUnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MongoUnitOfWork>());
        services.AddScoped<IMongoUnitOfWork>(sp => sp.GetRequiredService<MongoUnitOfWork>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        services.Configure<OutboxOptions>(options =>
        {
            if (TimeSpan.TryParse(
                    Environment.GetEnvironmentVariable(VariableNames.OutboxPollInterval),
                    CultureInfo.InvariantCulture,
                    out var interval))
                options.PollInterval = interval;

            if (int.TryParse(
                    Environment.GetEnvironmentVariable(VariableNames.OutboxBatchSize),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var batch))
                options.BatchSize = batch;

            if (int.TryParse(
                    Environment.GetEnvironmentVariable(VariableNames.OutboxMaxRetries),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var retries))
                options.MaxRetries = retries;

            if (int.TryParse(
                    Environment.GetEnvironmentVariable(VariableNames.OutboxProcessedRetentionDays),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var days))
                options.ProcessedRetention = TimeSpan.FromDays(days);

            if (TimeSpan.TryParse(
                    Environment.GetEnvironmentVariable(VariableNames.OutboxClaimTtl),
                    CultureInfo.InvariantCulture,
                    out var claimTtl))
                options.ClaimTtl = claimTtl;
        });

        services.AddSingleton<OutboxHealthMetrics>();
        services.AddHostedService<MongoOutboxProcessor>();
        services.AddHostedService<MongoOutboxCleanupService>();
        services.AddHostedService<MongoIndexInitializer>();
    }

    /// <summary>
    ///     Registra a `ConventionPack` global de Bson para esta API: camelCase nas keys,
    ///     enums como string, e ignora extras. Idempotente (registra uma vez por processo
    ///     via <see cref="Interlocked.Exchange(ref int, int)" />).
    /// </summary>
    /// <remarks>
    ///     Exposto como <see langword="internal"/> para que o teste de contrato em
    ///     `Mecanica.Hermes.Pagamento.Infrastructure.Tests.Persistence.MongoCamelCaseConventionTests`
    ///     consiga ativar as convenções antes de serializar um aggregate.
    /// </remarks>
    internal static void EnsureConventions()
    {
        if (Interlocked.Exchange(ref _conventionsRegistered, 1) == 1) return;

        // MongoDB.Driver 3.x removed BsonDefaults.GuidRepresentationMode/GuidRepresentationMode.V3.
        // The Standard representation is now the only supported mode and is the default.
        var pack = new ConventionPack
        {
            new IgnoreExtraElementsConvention(true),
            new EnumRepresentationConvention(BsonType.String),
            new CamelCaseElementNameConvention()
        };
        ConventionRegistry.Register("mecanica-hermes-pagamento-conventions", pack, _ => true);

        // Saga state PagamentoState.OperationTimeoutTokenId (Guid?) nao tem
        // [BsonGuidRepresentation]; sem o serializador global, MassTransit MongoDb
        // saga falha com BsonSerializationException e nenhum pagamento eh criado.
        MongoDB.Bson.Serialization.BsonSerializer.TryRegisterSerializer(
            new MongoDB.Bson.Serialization.Serializers.GuidSerializer(GuidRepresentation.Standard));
    }
}
