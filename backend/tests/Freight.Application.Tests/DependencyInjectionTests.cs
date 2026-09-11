using Freight.Application.Fleet;
using Freight.Application.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Freight.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersAKnownHandlerAsScoped()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(AddTruckHandler));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(AddTruckHandler), descriptor.ImplementationType);
    }

    [Fact]
    public void AddApplication_RegistersEveryHandlerInTheAssemblyExactlyOnce()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        var expectedHandlerTypes = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false, IsPublic: true }
                           && type.Name.EndsWith("Handler", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(expectedHandlerTypes);
        foreach (var handlerType in expectedHandlerTypes)
        {
            Assert.Single(services, d => d.ServiceType == handlerType);
        }
        Assert.Equal(expectedHandlerTypes.Count, services.Count);
    }

    [Fact]
    public void AddApplication_DoesNotRegisterNonHandlerPublicTypes()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        // Request/response records and DTOs live in the same namespaces but must not match
        // the "*Handler" naming filter.
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(AddTruckRequest));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ShipmentSummaryDto));
    }
}
