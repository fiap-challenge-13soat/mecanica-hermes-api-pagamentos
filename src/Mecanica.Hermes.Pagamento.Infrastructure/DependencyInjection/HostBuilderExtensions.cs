using System.Diagnostics.CodeAnalysis;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Mecanica.Hermes.Pagamento.Infrastructure.Autofac;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Mecanica.Hermes.Pagamento.Infrastructure.DependencyInjection;

[ExcludeFromCodeCoverage(Justification = "Composition root — registra Autofac via UseServiceProviderFactory.")]
public static class HostBuilderExtensions
{
    public static void ConfigureHostBuilder(this ConfigureHostBuilder hostBuilder)
    {
        hostBuilder.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        hostBuilder.ConfigureContainer<ContainerBuilder>(b => { b.RegisterModule<AutofacConfiguration>(); });
    }
}
