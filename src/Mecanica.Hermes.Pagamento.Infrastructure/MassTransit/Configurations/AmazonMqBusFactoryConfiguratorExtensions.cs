using System.Diagnostics.CodeAnalysis;
using System.Security.Authentication;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Configurations;

[ExcludeFromCodeCoverage(Justification = "Configuração de broker Amazon MQ (engine RabbitMQ) — exercitada em ambiente real.")]
internal static class AmazonMqBusFactoryConfiguratorExtensions
{
    // Amazon MQ for RabbitMQ recomenda heartbeat de 60s; valores menores derrubam a conexão
    // sob carga porque o broker gerenciado faz throttling agressivo em handshakes frequentes.
    private static readonly TimeSpan AmazonMqHeartbeat = TimeSpan.FromSeconds(60);

    public static void ConfigureAmazonMq(this IRabbitMqBusFactoryConfigurator configurator,
        IBusRegistrationContext registration)
    {
        var options = registration.GetRequiredService<IOptions<MassTransitOptions>>().Value;

        if (string.IsNullOrWhiteSpace(options.MassTransitConnectionString))
            throw new InvalidOperationException(
                "MASSTRANSIT__CONNECTION_STRING é obrigatória quando MASSTRANSIT__TRANSPORT_SERVICE=AmazonMq. " +
                "Use o endpoint AMQPS do Amazon MQ (amqps://user:pass@broker-id.mq.<region>.amazonaws.com:5671).");

        configurator.UseDelayedMessageScheduler();

        configurator.Host(new Uri(options.MassTransitConnectionString), h =>
        {
            h.Heartbeat(AmazonMqHeartbeat);
            h.UseSsl(s =>
            {
                s.Protocol = SslProtocols.Tls12;
            });
        });

        configurator.ConfigureEndpoints(registration);
        configurator.ConfigureCommonFactory(registration);
    }
}
