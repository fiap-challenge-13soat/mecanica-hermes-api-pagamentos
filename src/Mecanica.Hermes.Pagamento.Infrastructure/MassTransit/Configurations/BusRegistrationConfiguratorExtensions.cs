using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Mecanica.Hermes.Pagamento.Application;
using Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Saga;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Configurations;

[ExcludeFromCodeCoverage(Justification = "Composition root de SAGA/MongoDB — exercitado em integração.")]
internal static class BusRegistrationConfiguratorExtensions
{
    internal static IBusRegistrationConfigurator ConfigureSaga(
        this IBusRegistrationConfigurator configurator,
        string? mongoConnectionString,
        string mongoDatabaseName)
    {
        var saga = configurator.AddSagaStateMachine<PagamentoStateMachine, PagamentoState>();

        if (!string.IsNullOrWhiteSpace(mongoConnectionString))
            saga.MongoDbRepository(r =>
            {
                r.Connection = mongoConnectionString;
                r.DatabaseName = mongoDatabaseName;
                r.CollectionName = "pagamento-saga-state";
            });
        else
            saga.InMemoryRepository();

        configurator.AddConsumers(typeof(IApplicationAssemblyMarker).Assembly);

        configurator.AddDelayedMessageScheduler();
        return configurator;
    }
}
