using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Configurations;

[ExcludeFromCodeCoverage(Justification = "Configuração de broker RabbitMQ — exercitada em ambiente real.")]
internal static class RabbitMqBusFactoryConfiguratorExtensions
{
    public static void ConfigureRabbitMq(this IRabbitMqBusFactoryConfigurator configurator,
        IBusRegistrationContext registration)
    {
        var options = registration.GetRequiredService<IOptions<MassTransitOptions>>().Value;

        configurator.UseDelayedMessageScheduler();
        configurator.Host(options.MassTransitConnectionString);
        configurator.ConfigureEndpoints(registration);

        configurator.ConfigureCommonFactory(registration);
    }
}
