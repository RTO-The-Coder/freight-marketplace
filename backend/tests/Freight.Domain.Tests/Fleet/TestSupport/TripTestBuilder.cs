using Freight.Domain.Fleet;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests.Fleet.TestSupport;

/// <summary>
/// Shared construction helpers for Trip/Stop-based tests, so Trip's tests, Truck's
/// tests, and the two Fleet domain services (which all operate on the same shapes)
/// don't each reinvent their own fixture code.
/// </summary>
internal static class TripTestBuilder
{
    public static GeoLocation SomeLocation() => GeoLocation.Create(52.5200, 13.4050);

    public static GeoLocation OtherLocation() => GeoLocation.Create(48.1351, 11.5820);

    public static GeoLocation OfficeLocation() => GeoLocation.Create(50.1109, 8.6821);

    public static Capacity SomeLoad(double weightKg = 100, double volumeCubicMeters = 1) =>
        Capacity.Create(weightKg, volumeCubicMeters);

    public static Trip OpenTrip(Guid? truckId = null, Guid? truckingCompanyId = null, DateTime? startedAt = null) =>
        Trip.Open(
            truckId ?? Guid.NewGuid(),
            truckingCompanyId ?? Guid.NewGuid(),
            startedAt ?? new DateTime(2026, 1, 1, 6, 0, 0));

    /// <summary>A LegPlan for inserting a shipment at the end of the pending route (both follower legs null).</summary>
    public static LegPlan AppendLegPlan(
        double pickupIncomingKm = 50, int pickupIncomingTicks = 6,
        double deliveryIncomingKm = 80, int deliveryIncomingTicks = 10,
        double toOfficeKm = 40, int toOfficeTicks = 5) =>
        new(
            PickupIncoming: new RouteSegment(pickupIncomingKm, pickupIncomingTicks),
            PickupToFollower: null,
            DeliveryIncoming: new RouteSegment(deliveryIncomingKm, deliveryIncomingTicks),
            DeliveryToFollower: null,
            ToOffice: new RouteSegment(toOfficeKm, toOfficeTicks));

    /// <summary>A LegPlan for inserting a shipment mid-route (both follower legs supplied).</summary>
    public static LegPlan MidRouteLegPlan(
        double pickupIncomingKm = 20, int pickupIncomingTicks = 3,
        double pickupToFollowerKm = 15, int pickupToFollowerTicks = 2,
        double deliveryIncomingKm = 25, int deliveryIncomingTicks = 4,
        double deliveryToFollowerKm = 10, int deliveryToFollowerTicks = 1,
        double toOfficeKm = 40, int toOfficeTicks = 5) =>
        new(
            PickupIncoming: new RouteSegment(pickupIncomingKm, pickupIncomingTicks),
            PickupToFollower: new RouteSegment(pickupToFollowerKm, pickupToFollowerTicks),
            DeliveryIncoming: new RouteSegment(deliveryIncomingKm, deliveryIncomingTicks),
            DeliveryToFollower: new RouteSegment(deliveryToFollowerKm, deliveryToFollowerTicks),
            ToOffice: new RouteSegment(toOfficeKm, toOfficeTicks));

    /// <summary>Opens a trip and assigns one shipment appended at the end - the common "just need a populated trip" starting point.</summary>
    public static (Trip Trip, Guid ShipmentId) OpenTripWithOneShipment(Guid? truckId = null, GeoLocation? office = null)
    {
        var trip = OpenTrip(truckId: truckId);
        var shipmentId = Guid.NewGuid();

        trip.AssignShipment(
            shipmentId,
            SomeLoad(),
            SomeLocation(),
            OtherLocation(),
            office ?? OfficeLocation(),
            pickupInsertIndex: 0,
            deliveryInsertIndex: 0,
            AppendLegPlan());

        return (trip, shipmentId);
    }
}
