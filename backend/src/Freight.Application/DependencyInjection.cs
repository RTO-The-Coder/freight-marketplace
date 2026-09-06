using Microsoft.Extensions.DependencyInjection;

namespace Freight.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every use-case handler in this assembly as scoped. A handler is a public,
    /// concrete, non-generic class whose name ends in "Handler". Controllers depend on the
    /// concrete handler type, so each is registered as itself (no interface).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var handlers = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, IsPublic: true }
                           && type.Name.EndsWith("Handler", StringComparison.Ordinal));

        foreach (var handler in handlers)
        {
            services.AddScoped(handler);
        }

        return services;
    }
}
