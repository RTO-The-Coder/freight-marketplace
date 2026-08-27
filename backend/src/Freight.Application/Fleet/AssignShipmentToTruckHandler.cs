using Freight.Application.Shipments;
using Freight.Domain.Client;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Fleet;

public sealed record AssignShipmentToTruckRequest(Guid TruckId, Guid ShipmentId, int PickupInsertIndex, int DeliveryInsertIndex);

public sealed record AssignShipmentToTruckResponse(int StopCount);

/// <summary>
/// Assigns a booked shipment to a specific truck's route at caller-specified insertion
/// points - the same workflow Slice 12's offer-approval will later call as its final
/// step. Finds or opens the truck's current <see cref="Trip"/>, previews the insertion
/// on a clone (<see cref="Trip.Clone"/>) to run the route/window feasibility check
/// (<see cref="IShipmentInsertionEvaluator"/> - rejects if any downstream stop's
/// projected arrival would violate its own requested window), and only if feasible
/// performs the real insertion (<see cref="Truck.AssignShipment"/>) and starts the
/// truck's primary driver driving so their compliance ledger begins accumulating.
/// </summary>
public sealed class AssignShipmentToTruckHandler(
    IUnitOfWork unitOfWork,
    IShipmentInsertionEvaluator insertionEvaluator,
    TimeProvider timeProvider)
{
    // Hardcoded placeholders until OSRM/IRoutingService (Slice 7) computes real
    // distance/time for each leg. 78 ticks = 6h30m at the fixed 5-minute tick size (see
    // RouteProgress.TotalTimeTick).
    private const double PlaceholderLegDistanceKm = 650;
    private const int PlaceholderLegTimeTicks = 78;

    public async Task<AssignShipmentToTruckResponse> AssignShipment(AssignShipmentToTruckRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        var shipment = await unitOfWork.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment '{request.ShipmentId}' was not found.");

        if (truck.TruckingCompanyId is null)
        {
            throw new InvalidOperationException("Truck must belong to a trucking company before it can accept a shipment.");
        }

        if (!truck.IsActive)
        {
            throw new InvalidOperationException("Truck must be active to accept a shipment.");
        }

        if (truck.Type != shipment.RequiredTruckType)
        {
            throw new InvalidOperationException(
                $"Truck type mismatch: this shipment requires a {shipment.RequiredTruckType} truck, but {truck.TruckName} is a {truck.Type}.");
        }

        if (truck.DriverAssignment is null)
        {
            throw new InvalidOperationException("Truck must have a driver assigned before it can accept a shipment.");
        }

        var company = await unitOfWork.TruckingCompanies.GetByIdAsync(truck.TruckingCompanyId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Trucking company '{truck.TruckingCompanyId.Value}' was not found.");

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);
        var isNewTrip = trip is null;
        trip ??= Trip.Open(truck.Id, company.Id, now);

        var pendingStops = trip.Stops.Count(x => x.Status == StopStatus.Reached);
        if (request.PickupInsertIndex < 0 || request.PickupInsertIndex > pendingStops)
        {
            throw new ArgumentOutOfRangeException(nameof(request.PickupInsertIndex), request.PickupInsertIndex,
                "Pickup insertion index is out of range for the current route.");
        }

        if (request.DeliveryInsertIndex < 0 || request.DeliveryInsertIndex > pendingStops)
        {
            throw new ArgumentOutOfRangeException(nameof(request.DeliveryInsertIndex), request.DeliveryInsertIndex,
                "Delivery insertion index is out of range for the current route.");
        }

        // Fresh GeoLocation/Capacity instances, not the tracked ones off Shipment/
        // TruckingCompany - EF Core's change tracker treats owned-type instances by
        // reference identity, so reusing shipment.PickupLocation etc. directly here
        // makes it conflate Stop's copy with Shipment's/TruckingCompany's owned
        // navigation of the same CLR type, corrupting insert/update detection for the
        // newly-created Stop rows (they get emitted as UPDATEs instead of INSERTs,
        // affecting 0 rows and throwing DbUpdateConcurrencyException).
        var pickupLocation = GeoLocation.Create(shipment.PickupLocation.Latitude, shipment.PickupLocation.Longitude);
        var deliveryLocation = GeoLocation.Create(shipment.DeliveryLocation.Latitude, shipment.DeliveryLocation.Longitude);
        var officeLocation = GeoLocation.Create(company.OfficeLocation.Latitude, company.OfficeLocation.Longitude);

        var shipmentSize = Capacity.Create(shipment.Load.WeightKg, shipment.Load.VolumeCubicMeters);

        // Preview the insertion on a throwaway clone so feasibility runs against the
        // route as it WOULD look, without mutating the real trip.
        var preview = trip.Clone();
        preview.AssignShipment(
            shipment.Id, shipmentSize, pickupLocation, deliveryLocation, officeLocation,
            request.PickupInsertIndex, request.DeliveryInsertIndex,
            PlaceholderLegDistanceKm, PlaceholderLegTimeTicks,
            PlaceholderLegDistanceKm, PlaceholderLegTimeTicks,
            PlaceholderLegDistanceKm, PlaceholderLegTimeTicks);

        var feasibility = insertionEvaluator.Evaluate(preview.Stops, truck.Capacity);

        if (!feasibility.IsFeasible)
        {
            throw new InvalidOperationException(
                $"Cannot assign shipment '{shipment.Id}' to {truck.TruckName}: {feasibility.ViolationReason}");
        }

        // Feasible - apply the same insertion to the real trip.
        if (isNewTrip)
        {
            unitOfWork.Trips.Add(trip);
        }

        var previousNextStopId = trip.NextStop?.Id;

        trip.AssignShipment(
            shipment.Id, shipmentSize, pickupLocation, deliveryLocation, officeLocation,
            request.PickupInsertIndex, request.DeliveryInsertIndex,
            PlaceholderLegDistanceKm, PlaceholderLegTimeTicks,
            PlaceholderLegDistanceKm, PlaceholderLegTimeTicks,
            PlaceholderLegDistanceKm, PlaceholderLegTimeTicks);

        truck.SyncProgressToNextStop(trip, previousNextStopId);

        shipment.AssignToCompany(truck.TruckingCompanyId.Value);

        truck.DriverAssignment.PrimaryDriver.StartDriving(now);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssignShipmentToTruckResponse(trip.Stops.Count);
    }

    /// <summary>
    /// Builds the window lookup <see cref="IShipmentInsertionEvaluator.Evaluate"/> needs
    /// for every Pending stop in <paramref name="preview"/> - the newly-inserted
    /// shipment's own windows are already known (<paramref name="newShipment"/>); every
    /// other Pending Pickup/Delivery stop's window is looked up from its own Shipment.
    /// </summary>
    private async Task<Dictionary<Guid, TimeWindow>> BuildShipmentWindowsAsync(
        Trip preview, Shipment newShipment, CancellationToken cancellationToken)
    {
        var windows = new Dictionary<Guid, TimeWindow>();

        foreach (var stop in preview.Stops)
        {
            if (stop.Status != StopStatus.Pending || stop.Kind == StopKind.Office || stop.ShipmentId is not { } shipmentId)
            {
                continue;
            }

            if (shipmentId == newShipment.Id)
            {
                windows[stop.Id] = stop.Kind == StopKind.Pickup ? newShipment.PickupWindow : newShipment.DeliveryWindow;
                continue;
            }

            var existingShipment = await unitOfWork.Shipments.GetByIdAsync(shipmentId, cancellationToken)
                ?? throw new InvalidOperationException($"Shipment '{shipmentId}' referenced by stop '{stop.Id}' was not found.");

            windows[stop.Id] = stop.Kind == StopKind.Pickup ? existingShipment.PickupWindow : existingShipment.DeliveryWindow;
        }

        return windows;
    }
}
