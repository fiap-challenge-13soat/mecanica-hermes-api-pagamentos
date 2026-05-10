using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Autofac;
using Mecanica.Hermes.Pagamento.Application.Common.Messaging;
using Mecanica.Hermes.Shared.Application.Messaging;
using Mecanica.Hermes.Shared.Core.Results;
using MediatR;
using Module = Autofac.Module;

namespace Mecanica.Hermes.Pagamento.Infrastructure.Autofac;

[ExcludeFromCodeCoverage(Justification = "Composition root — registra repos e AsyncCommandHandlers via reflection.")]
internal class AutofacConfiguration : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(c => c.FullName!.Contains("Mecanica.Hermes.Pagamento", StringComparison.Ordinal))
            .ToArray();

        builder.RegisterAssemblyTypes(assemblies)
            .Where(t => t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        RegisterAsyncCommandHandlers(builder, assemblies);
    }

    /// <summary>
    ///     Registra um <see cref="AsyncCommandHandler{TRequest}" /> fechado para cada
    ///     <see cref="IAsyncCommand" /> encontrado nos assemblies da aplicação.
    /// </summary>
    private static void RegisterAsyncCommandHandlers(ContainerBuilder builder, IEnumerable<Assembly> assemblies)
    {
        var asyncCommandTypes = assemblies
            .SelectMany(SafeGetTypes)
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IAsyncCommand).IsAssignableFrom(t));

        foreach (var commandType in asyncCommandTypes)
        {
            var handlerType = typeof(AsyncCommandHandler<>).MakeGenericType(commandType);
            var serviceType = typeof(IRequestHandler<,>).MakeGenericType(commandType, typeof(Result));

            builder.RegisterType(handlerType)
                .As(serviceType)
                .InstancePerDependency();
        }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
