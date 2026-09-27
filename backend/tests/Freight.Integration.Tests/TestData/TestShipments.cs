using Freight.Api.Controllers;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;

namespace Freight.Integration.Tests.TestData;

/// <summary>Ready-made POST /shipments bodies. Everything except points, load and windows is fixed.</summary>
public static class TestShipments
{
    private static readonly TimeSpan OpensBeforeArrival = TimeSpan.FromHours(6);
    private static readonly TimeSpan ClosesAfterArrival = TimeSpan.FromHours(12);

    /// <summary>A refrigerated shipment from <paramref name="pickup"/> to <paramref name="delivery"/>.</summary>
    public static BookShipmentBody Between(
        Guid shipperId, GeoLocation pickup, GeoLocation delivery, Capacity load, TimeWindow pickupWindow, TimeWindow deliveryWindow) =>
        new(
            shipperId,
            pickup.Latitude,
            pickup.Longitude,
            delivery.Latitude,
            delivery.Longitude,
            load.WeightKg,
            load.VolumeCubicMeters,
            TruckType.Refrigerated,
            pickupWindow.Earliest,
            pickupWindow.Latest,
            deliveryWindow.Earliest,
            deliveryWindow.Latest);

    /// <summary>
    /// A realistic window around a stop's expected arrival: opens 6h before, closes 12h after.
    /// The truck never arrives before it opens, so it never has to park and wait.
    /// </summary>
    public static TimeWindow WindowAround(DateTime expectedArrival) =>
        TimeWindow.Create(expectedArrival - OpensBeforeArrival, expectedArrival + ClosesAfterArrival);
}
