using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Mecanica.Hermes.Pagamento.Application.Pagamentos.Messages.Commands;
using Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mecanica.Hermes.Pagamento.Infrastructure.MassTransit.Configurations;

[ExcludeFromCodeCoverage(Justification = "Configuração de retry/redelivery — exercitada pelo broker em produção.")]
internal static class BusFactoryConfiguratorExtensions
{
    private const int ImmediateRetryCount = 3;
    private const int DelayedRetryLimit = 3;
    private static readonly TimeSpan ImmediateRetryInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DelayedMinInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DelayedMaxInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DelayedIntervalDelta = TimeSpan.FromSeconds(15);

    public static void ConfigureCommonFactory(this IBusFactoryConfigurator configurator,
        IBusRegistrationContext registration)
    {
        var options = registration.GetRequiredService<IOptions<MassTransitOptions>>().Value;

        configurator.PrefetchCount = options.MassTransitPrefetchCount;

        configurator.UseMessageRetry(r =>
        {
            r.Ignore<ArgumentException>();
            r.Ignore<InvalidOperationException>();
            r.Handle<TimeoutException>();
            r.Handle<HttpRequestException>();
            r.Interval(ImmediateRetryCount, ImmediateRetryInterval);
        });

        configurator.UseDelayedRedelivery(r =>
        {
            r.Ignore<ArgumentException>();
            r.Ignore<InvalidOperationException>();
            r.Handle<TimeoutException>();
            r.Handle<HttpRequestException>();
            r.Exponential(
                DelayedRetryLimit,
                DelayedMinInterval,
                DelayedMaxInterval,
                DelayedIntervalDelta);
        });

        configurator.UseInMemoryOutbox(registration);

        configurator.UseConsumeFilter(typeof(PagamentoCommandValidationFilter<>), registration,
            x => x.Include(type => typeof(IPagamentoExecuteCommand).IsAssignableFrom(type)));
    }
}
