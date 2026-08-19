using System.Net;
using System.Net.Http.Json;
using Freight.Domain.Fleet;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Freight.Api.Tests;

public sealed class ActivateDeactivateTruckEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public ActivateDeactivateTruckEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostActivate_TruckWithoutCompany_Returns400WithMessage()
    {
        var truck = Truck.Create("No Company Truck", TruckType.BoxVan, TruckSize.Small);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Truck>().Add(truck);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.PostAsync($"/trucks/{truck.Id}/activate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.Contains("company", body!["error"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostActivate_TruckWithCompany_Returns204AndPersistsActive()
    {
        var truck = Truck.Create("With Company Truck", TruckType.BoxVan, TruckSize.Small);
        truck.AssignToCompany(Guid.NewGuid());

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Truck>().Add(truck);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.PostAsync($"/trucks/{truck.Id}/activate", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = _factory.Services.CreateScope();
        var readContext = scope2.ServiceProvider.GetRequiredService<FreightDbContext>();
        var persisted = await readContext.Set<Truck>().FirstAsync(t => t.Id == truck.Id);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task PostDeactivate_ActiveTruck_Returns204AndPersistsInactive()
    {
        var truck = Truck.Create("Active Truck", TruckType.BoxVan, TruckSize.Small);
        truck.AssignToCompany(Guid.NewGuid());
        truck.Activate();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Truck>().Add(truck);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.PostAsync($"/trucks/{truck.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = _factory.Services.CreateScope();
        var readContext = scope2.ServiceProvider.GetRequiredService<FreightDbContext>();
        var persisted = await readContext.Set<Truck>().FirstAsync(t => t.Id == truck.Id);
        Assert.False(persisted.IsActive);
    }
}
