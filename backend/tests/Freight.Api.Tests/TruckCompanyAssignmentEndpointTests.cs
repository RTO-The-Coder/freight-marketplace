using System.Net;
using System.Net.Http.Json;
using Freight.Domain.Fleet;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Freight.Api.Tests;

public sealed class TruckCompanyAssignmentEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public TruckCompanyAssignmentEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record AssignTruckToCompanyBody(Guid TruckingCompanyId);

    [Fact]
    public async Task PostCompany_UnassignedTruck_Returns204AndPersistsCompany()
    {
        var truck = Truck.Create("Truck A", TruckType.BoxVan, TruckSize.Small);
        var companyId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Truck>().Add(truck);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/trucks/{truck.Id}/company", new AssignTruckToCompanyBody(companyId));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = _factory.Services.CreateScope();
        var readContext = scope2.ServiceProvider.GetRequiredService<FreightDbContext>();
        var persisted = await readContext.Set<Truck>().FirstAsync(t => t.Id == truck.Id);
        Assert.Equal(companyId, persisted.TruckingCompanyId);
    }

    [Fact]
    public async Task DeleteCompany_AssignedTruck_Returns204AndPersistsNullCompany()
    {
        var truck = Truck.Create("Truck B", TruckType.BoxVan, TruckSize.Small);
        truck.AssignToCompany(Guid.NewGuid());

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Truck>().Add(truck);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.DeleteAsync($"/trucks/{truck.Id}/company");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = _factory.Services.CreateScope();
        var readContext = scope2.ServiceProvider.GetRequiredService<FreightDbContext>();
        var persisted = await readContext.Set<Truck>().FirstAsync(t => t.Id == truck.Id);
        Assert.Null(persisted.TruckingCompanyId);
    }
}
