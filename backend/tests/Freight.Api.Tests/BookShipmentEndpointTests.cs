using System.Net;
using System.Net.Http.Json;
using Freight.Domain.Fleet;
using Freight.Domain.Shipment;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShipmentAggregate = Freight.Domain.Shipment.Shipment;

namespace Freight.Api.Tests;

public sealed class BookShipmentEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public BookShipmentEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record BookShipmentBody(
        Guid ShipperId,
        double PickupLatitude,
        double PickupLongitude,
        double DeliveryLatitude,
        double DeliveryLongitude,
        double LoadWeightKg,
        double LoadVolumeCubicMeters,
        TruckType RequiredTruckType,
        DateTime PickupWindowEarliest,
        DateTime PickupWindowLatest,
        DateTime DeliveryWindowEarliest,
        DateTime DeliveryWindowLatest);

    private sealed record UpdatePickupWindowBody(DateTime PickupWindowEarliest, DateTime PickupWindowLatest);

    private sealed record BookShipmentResponse(Guid ShipmentId);

    private static BookShipmentBody ValidBody(Guid shipperId) => new(
        shipperId,
        52.5200, 13.4050,
        48.1351, 11.5820,
        500, 5,
        TruckType.Flatbed,
        new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 1, 2, 14, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 1, 2, 18, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task PostShipment_ValidRequest_Returns200WithPendingShipment()
    {
        using var client = _factory.CreateClient();
        var shipperId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/shipments", ValidBody(shipperId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BookShipmentResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.ShipmentId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        var persisted = await dbContext.Set<ShipmentAggregate>().FirstAsync(s => s.Id == body.ShipmentId);
        Assert.Equal(shipperId, persisted.ShipperId);
        Assert.Null(persisted.TruckingCompanyId);
        Assert.Equal(ShipmentStatus.Pending, persisted.Status);
    }

    [Fact]
    public async Task PatchPickupWindow_PendingShipment_Returns204AndUpdatesWindow()
    {
        using var client = _factory.CreateClient();
        var bookResponse = await client.PostAsJsonAsync("/shipments", ValidBody(Guid.NewGuid()));
        var booked = await bookResponse.Content.ReadFromJsonAsync<BookShipmentResponse>();

        var newWindow = new UpdatePickupWindowBody(
            new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc));

        var response = await client.PatchAsJsonAsync($"/shipments/{booked!.ShipmentId}/pickup-window", newWindow);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        var persisted = await dbContext.Set<ShipmentAggregate>().FirstAsync(s => s.Id == booked.ShipmentId);
        Assert.Equal(newWindow.PickupWindowEarliest, persisted.PickupWindow.Earliest);
        Assert.Equal(newWindow.PickupWindowLatest, persisted.PickupWindow.Latest);
    }
}
