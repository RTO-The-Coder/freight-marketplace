using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private sealed record AddTruckBody(string TruckName, TruckType TruckType, TruckSize TruckSize);

    private sealed record AddTruckResponse(
        Guid TruckId,
        string TruckName,
        TruckType TruckType,
        TruckSize TruckSize,
        bool IsActive,
        Guid? TruckingCompanyId);

    [Fact]
    public async Task PostTruck_ValidRequest_Returns201WithPersistedUnassignedTruck()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/trucks",
            new AddTruckBody("Integration Truck", TruckType.BoxVan, TruckSize.Medium));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<AddTruckResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.TruckId);
        Assert.Equal("Integration Truck", body.TruckName);
        Assert.Equal(TruckType.BoxVan, body.TruckType);
        Assert.Equal(TruckSize.Medium, body.TruckSize);
        Assert.False(body.IsActive);
        Assert.Null(body.TruckingCompanyId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        var persisted = await dbContext.Set<Truck>().FirstAsync(t => t.Id == body.TruckId);
        Assert.Null(persisted.TruckingCompanyId);
    }
}
