using System.Text.Json;
using System.Text.Json.Serialization;
using Freight.Api.Controllers;
using Freight.Domain.Client.Enums;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Xunit.Abstractions;

namespace Freight.Integration.Tests.TestSupport;

/// <summary>
/// Base for every scenario test class. xUnit creates a new instance per test method, so each
/// test gets its own <see cref="IntegrationTestFactory"/> (and so its own throwaway database)
/// built around the router its scenario passes in, plus an <see cref="HttpClient"/> against
/// the in-process API.
///
/// The database is dropped only when the test calls <see cref="MarkPassed"/>. A failing test
/// never reaches that call, so its database is kept - with its name written to the test
/// output - for inspecting the exact state the failure left behind.
/// </summary>
public abstract class IntegrationTestBase(IRoutingService routingService, ITestOutputHelper output) : IAsyncLifetime
{
    /// <summary>Mirrors Program.cs: the API reads and writes enums as strings.</summary>
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private bool _passed;

    protected IntegrationTestFactory Factory { get; } = new(routingService);
    protected HttpClient Client { get; private set; } = null!;
    protected FreightApi Api { get; private set; } = null!;
    protected ITestOutputHelper Output { get; } = output;

    /// <summary>Call as the last line of a test. Without it the database is kept.</summary>
    protected void MarkPassed() => _passed = true;

    /// <summary>
    /// Clock at <paramref name="start"/>, an office at <see cref="TestPoints.Office"/>, a
    /// shipper, and an active Medium truck with one driver.
    /// </summary>
    protected async Task<(Guid TruckId, Guid DriverId, Guid ShipperId)> SetUpSingleDriverAsync(DateTime start, AddDriverBody driver)
    {
        await Api.SetClockAsync(start);
        var company = await Factory.SeedTruckingCompanyAsync(TestPoints.Office);
        var shipper = await Factory.SeedShipperAsync();

        var truckId = await Api.AddTruckAsync(TestTrucks.MediumRefrigerated());
        await Api.AssignTruckToCompanyAsync(truckId, company.Id);
        var driverId = await Api.AddDriverAsync(driver);
        await Api.AssignDriversAsync(truckId, driverId);
        await Api.ActivateTruckAsync(truckId);

        return (truckId, driverId, shipper.Id);
    }

    /// <summary>As <see cref="SetUpSingleDriverAsync"/>, with a Large truck and a team: A primary, B secondary.</summary>
    protected async Task<(Guid TruckId, Guid DriverA, Guid DriverB, Guid ShipperId)> SetUpTeamAsync(
        DateTime start, AddDriverBody driverA, AddDriverBody driverB)
    {
        await Api.SetClockAsync(start);
        var company = await Factory.SeedTruckingCompanyAsync(TestPoints.Office);
        var shipper = await Factory.SeedShipperAsync();

        var truckId = await Api.AddTruckAsync(TestTrucks.LargeRefrigerated());
        await Api.AssignTruckToCompanyAsync(truckId, company.Id);
        var driverAId = await Api.AddDriverAsync(driverA);
        var driverBId = await Api.AddDriverAsync(driverB);
        await Api.AssignDriversAsync(truckId, driverAId, driverBId);
        await Api.ActivateTruckAsync(truckId);

        return (truckId, driverAId, driverBId, shipper.Id);
    }

    /// <summary>Books a shipment and assigns it to the truck at the given insert indexes.</summary>
    protected async Task<Guid> BookAndAssignAsync(
        Guid shipperId, Guid truckId, GeoLocation pickup, GeoLocation delivery, Capacity load,
        TimeWindow pickupWindow, TimeWindow deliveryWindow, int pickupIndex, int deliveryIndex, DateTime? tripStartTime = null)
    {
        var shipmentId = await Api.BookShipmentAsync(
            TestShipments.Between(shipperId, pickup, delivery, load, pickupWindow, deliveryWindow));
        await Api.AssignShipmentAsync(truckId, shipmentId, pickupIndex, deliveryIndex, tripStartTime);
        return shipmentId;
    }

    /// <summary>
    /// Sequential shipments P1 → P2, P3 → P4, ... each appended at the end, with windows
    /// -6h / +12h around the hand-worked pickup and delivery times.
    /// </summary>
    protected async Task AssignSequentialAsync(
        ScenarioJourney journey, Guid shipperId, Guid truckId, (double Pickup, double Delivery)[] arrivals, Capacity? load = null)
    {
        for (var k = 0; k < arrivals.Length; k++)
        {
            await BookAndAssignAsync(
                shipperId, truckId, TestPoints.P(2 * k + 1), TestPoints.P(2 * k + 2), load ?? LoadSizes.Medium,
                TestShipments.WindowAround(journey.At(arrivals[k].Pickup)),
                TestShipments.WindowAround(journey.At(arrivals[k].Delivery)),
                pickupIndex: 2 * k, deliveryIndex: 2 * k);
        }
    }

    /// <summary>Every shipment of the shipper is delivered.</summary>
    protected async Task AssertAllDeliveredAsync(Guid shipperId)
    {
        var shipments = await Api.GetShipperShipmentsAsync(shipperId);
        Assert.All(shipments.Shipments, shipment => Assert.Equal(ShipmentStatus.Delivered, shipment.Status));
    }

    public async Task InitializeAsync()
    {
        // Migrate before creating the client - creating it boots the app against this database.
        await Factory.CreateDatabaseAsync();
        Client = Factory.CreateClient();
        Api = new FreightApi(Client, JsonOptions);
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();

        if (_passed)
        {
            await Factory.DropDatabaseAsync();
        }
        else
        {
            Output.WriteLine($"Test did not pass - kept database '{Factory.DatabaseName}' for inspection.");
        }

        await Factory.DisposeAsync();
    }
}
