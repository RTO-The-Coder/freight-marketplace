using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Freight.Domain.Fleet;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShipmentAggregate = Freight.Domain.Shipment.Shipment;
using ShipperAggregate = Freight.Domain.Shipment.Shipper;

namespace Freight.Api.Tests;

public sealed class ShippersEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public ShippersEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

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

    private sealed record ShipperSummaryDto(Guid ShipperId, string Name, string ContactEmail);

    private sealed record GetShippersResponse(IReadOnlyList<ShipperSummaryDto> Shippers);

    private sealed record ShipmentSummaryDto(Guid ShipmentId, Guid? TruckingCompanyId, string Status);

    private sealed record GetShipmentsByShipperResponse(IReadOnlyList<ShipmentSummaryDto> Shipments);

    [Fact]
    public async Task GetShippers_ReturnsPersistedShippers()
    {
        var shipper = ShipperAggregate.Create(Guid.NewGuid(), "Acme Cargo", "ops@acme.example.com");

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<ShipperAggregate>().Add(shipper);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/shippers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetShippersResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Contains(body!.Shippers, s => s.ShipperId == shipper.Id && s.Name == "Acme Cargo");
    }

    [Fact]
    public async Task GetShipmentsByShipper_ReturnsOnlyThatShippersShipments()
    {
        var shipper = ShipperAggregate.Create(Guid.NewGuid(), "Globex Freight", "shipping@globex.example.com");
        var otherShipper = ShipperAggregate.Create(Guid.NewGuid(), "Other Shipper", "other@example.com");

        var shipment = ShipmentAggregate.Book(
            shipper.Id,
            Freight.Domain.ValueObjects.GeoLocation.Create(52.5200, 13.4050),
            Freight.Domain.ValueObjects.GeoLocation.Create(48.1351, 11.5820),
            Freight.Domain.ValueObjects.Capacity.Create(500, 5),
            TruckType.Flatbed,
            Freight.Domain.ValueObjects.TimeWindow.Create(
                new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc)),
            Freight.Domain.ValueObjects.TimeWindow.Create(
                new DateTime(2026, 1, 2, 14, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 2, 18, 0, 0, DateTimeKind.Utc)),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var otherShipment = ShipmentAggregate.Book(
            otherShipper.Id,
            Freight.Domain.ValueObjects.GeoLocation.Create(52.5200, 13.4050),
            Freight.Domain.ValueObjects.GeoLocation.Create(48.1351, 11.5820),
            Freight.Domain.ValueObjects.Capacity.Create(500, 5),
            TruckType.Flatbed,
            Freight.Domain.ValueObjects.TimeWindow.Create(
                new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc)),
            Freight.Domain.ValueObjects.TimeWindow.Create(
                new DateTime(2026, 1, 2, 14, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 2, 18, 0, 0, DateTimeKind.Utc)),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<ShipperAggregate>().AddRange(shipper, otherShipper);
            dbContext.Set<ShipmentAggregate>().AddRange(shipment, otherShipment);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/shippers/{shipper.Id}/shipments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetShipmentsByShipperResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Contains(body!.Shipments, s => s.ShipmentId == shipment.Id);
        Assert.DoesNotContain(body.Shipments, s => s.ShipmentId == otherShipment.Id);
    }
}
