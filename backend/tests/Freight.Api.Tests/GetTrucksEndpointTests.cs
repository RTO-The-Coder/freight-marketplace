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

public sealed class GetTrucksEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public GetTrucksEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed record TruckSummaryDto(
        Guid TruckId,
        string TruckName,
        TruckType TruckType,
        TruckSize TruckSize,
        bool IsActive,
        TruckStatus Status,
        Guid? TruckingCompanyId,
        bool HasDriverAssignment);

    private sealed record GetTrucksResponse(IReadOnlyList<TruckSummaryDto> Trucks);

    [Fact]
    public async Task GetTrucks_UnassignedTrue_IncludesUnassignedExcludesAssigned()
    {
        var companyId = Guid.NewGuid();
        var unassignedTruck = Truck.Create("Unassigned Truck", TruckType.BoxVan, TruckSize.Small);
        var assignedTruck = Truck.Create("Assigned Truck", TruckType.BoxVan, TruckSize.Small);
        assignedTruck.AssignToCompany(companyId);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Truck>().AddRange(unassignedTruck, assignedTruck);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/trucks?unassigned=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetTrucksResponse>(JsonOptions);
        Assert.NotNull(body);

        Assert.Contains(body!.Trucks, t => t.TruckId == unassignedTruck.Id);
        Assert.DoesNotContain(body.Trucks, t => t.TruckId == assignedTruck.Id);
    }

    [Fact]
    public async Task GetTrucks_TruckingCompanyIdFilter_ReturnsOnlyThatCompanysTruck()
    {
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var matchingTruck = Truck.Create("Matching Truck", TruckType.BoxVan, TruckSize.Small);
        matchingTruck.AssignToCompany(companyId);
        var otherTruck = Truck.Create("Other Truck", TruckType.BoxVan, TruckSize.Small);
        otherTruck.AssignToCompany(otherCompanyId);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Truck>().AddRange(matchingTruck, otherTruck);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/trucks?truckingCompanyId={companyId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetTrucksResponse>(JsonOptions);
        Assert.NotNull(body);

        Assert.Contains(body!.Trucks, t => t.TruckId == matchingTruck.Id);
        Assert.DoesNotContain(body.Trucks, t => t.TruckId == otherTruck.Id);
    }
}
