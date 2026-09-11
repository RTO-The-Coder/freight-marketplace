using Freight.Domain.Client;
using Freight.Domain.Client.Enums;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using static Freight.Domain.Tests.Fleet.TestSupport.TripTestBuilder;

namespace Freight.Domain.Tests.Integration;

/// <summary>
/// Scenario 5 from the test plan: wires Shipment, Shipper and Trip/Stop together across a
/// full booking-to-delivery lifecycle. Shipment's status machine and Trip/Stop's status
/// machine are two ENTIRELY SEPARATE state machines - Stop.ShipmentId is a loose
/// reference, and they only move in lockstep because application-layer handlers call
/// both at the right moments (not enforced by the domain itself). This is the one place
/// that assumption is proven for a full lifecycle - neither ShipmentTests nor TripTests
/// alone can catch a divergence here, since each treats the other side as an opaque Guid.
/// </summary>
public class ShipmentTripLifecycleConsistencyTests
{
    private static readonly DateTime BookedAt = new(2026, 1, 1, 8, 0, 0);

    [Fact]
    public void BookingToDelivery_ShipmentStatusAndTripLoadStayInLockstepAtEveryTransition()
    {
        var shipper = Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.com");
        var shipment = Shipment.Book(
            Guid.NewGuid(), shipper.Id, SomeLocation(), OtherLocation(), SomeLoad(),
            TruckType.Refrigerated,
            TimeWindow.Create(BookedAt.AddHours(1), BookedAt.AddHours(3)),
            TimeWindow.Create(BookedAt.AddHours(5), BookedAt.AddHours(7)),
            BookedAt);

        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        var trip = OpenTrip(truckId: truck.Id, startedAt: BookedAt);

        // --- Booking: Shipment Pending -> Booked, independent of the trip. ---
        Assert.Equal(ShipmentStatus.Pending, shipment.Status);
        var companyId = Guid.NewGuid();
        shipment.AssignToCompany(companyId);
        Assert.Equal(ShipmentStatus.Booked, shipment.Status);

        // Insert this shipment's stops onto the real trip (the loose ShipmentId link).
        trip.AssignShipment(
            shipment.Id, shipment.Load, shipment.PickupLocation, shipment.DeliveryLocation, OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, AppendLegPlan());
        var pickupStop = trip.Stops.First(s => s.ShipmentId == shipment.Id && s.Kind == StopKind.Pickup);
        var deliveryStop = trip.Stops.First(s => s.ShipmentId == shipment.Id && s.Kind == StopKind.Delivery);

        // Before pickup: nothing on board yet, on either side.
        Assert.Equal(0, trip.CurrentLoad.WeightKg);

        // --- Pickup: mark the Trip's stop Reached AND the Shipment picked up - two
        // independent calls, on two independent aggregates, that a caller must make
        // together for the two sides to stay consistent. ---
        var pickupAt = BookedAt.AddHours(2);
        trip.MarkStopReached(pickupStop.Id, pickupAt);
        shipment.MarkPickedUp(pickupAt);

        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
        // Exactly when the Trip-side pickup stop was marked Reached, the Trip's derived
        // CurrentLoad independently picked up the same shipment's weight - both sides agree,
        // even though Trip.CurrentLoad has zero knowledge of Shipment.Status.
        Assert.Equal(shipment.Load.WeightKg, trip.CurrentLoad.WeightKg);
        Assert.Equal(shipment.Load.VolumeCubicMeters, trip.CurrentLoad.VolumeCubicMeters);

        // --- Delivery: same pairing. ---
        var deliveryAt = BookedAt.AddHours(6);
        trip.MarkStopReached(deliveryStop.Id, deliveryAt);
        shipment.MarkDelivered(deliveryAt);

        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
        // The moment Trip's delivery stop was marked Reached, CurrentLoad independently
        // dropped back to zero at the exact same moment Shipment reached Delivered.
        Assert.Equal(0, trip.CurrentLoad.WeightKg);
        Assert.Equal(0, trip.CurrentLoad.VolumeCubicMeters);
    }

    [Fact]
    public void ShipmentStatusMachine_IsIndependentOfTripStopStatus_MismatchIsPossibleIfCallerForgetsOneSide()
    {
        // Demonstrates the loose coupling explicitly: advancing ONLY the Shipment side
        // (never touching the Trip) succeeds without error, because the two state machines
        // do not enforce consistency with each other - only a caller convention does.
        var shipper = Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.com");
        var shipment = Shipment.Book(
            Guid.NewGuid(), shipper.Id, SomeLocation(), OtherLocation(), SomeLoad(),
            TruckType.Refrigerated,
            TimeWindow.Create(BookedAt.AddHours(1), BookedAt.AddHours(3)),
            TimeWindow.Create(BookedAt.AddHours(5), BookedAt.AddHours(7)),
            BookedAt);

        shipment.AssignToCompany(Guid.NewGuid());
        shipment.MarkPickedUp(BookedAt.AddHours(2));
        shipment.MarkDelivered(BookedAt.AddHours(6));

        // No Trip/Stop was ever touched - Shipment's own status machine doesn't require it.
        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
    }
}
