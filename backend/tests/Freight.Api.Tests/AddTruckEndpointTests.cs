using System.Net;
using System.Net.Http.Json;
using Freight.Domain.Fleet;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Freight.Api.Tests;

public sealed class AddTruckEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public AddTruckEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record AddTruckBody(string TruckName, TruckType TruckType, TruckSize TruckSize);

    private sealed record AddTruckResponse(Guid TruckId);

    [Fact]
    public async Task PostTruck_ValidRequest_Returns200WithPersistedUnassignedTruck()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/trucks",
            new AddTruckBody("Integration Truck", TruckType.BoxVan, TruckSize.Medium));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AddTruckResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.TruckId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        var persisted = await dbContext.Set<Truck>().FirstAsync(t => t.Id == body.TruckId);
        Assert.Null(persisted.TruckingCompanyId);
    }
}
