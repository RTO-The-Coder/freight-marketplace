using Freight.Domain.Common;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Fleet;

public sealed record AssignShipmentToTruckRequest(Guid TruckId, Guid ShipmentId);

public sealed record AssignShipmentToTruckResponse(int StopCount);

/// <summary>
/// Directly assigns a booked shipment to a specific truck's route - the same workflow
/// Slice 12's offer-approval will later call as its final step. Inserts a Pickup +
/// Delivery stop pair (and, the first time this truck receives a shipment, a single
/// always-last Office stop - see <see cref="Domain.Fleet.Truck.AssignShipment"/>), and
/// starts the truck's primary driver driving so their compliance ledger begins
/// accumulating.
/// </summary>
public sealed class AssignShipmentToTruckHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    // Hardcoded placeholders until OSRM/IRoutingService (Slice 7) computes real
    // distance/time for the leg to the newly-assigned pickup. 78 ticks = 6h30m at the
    // fixed 5-minute tick size (see RouteProgress.TotalTimeTick).
    private const double PlaceholderLegDistanceKm = 650;
    private const int PlaceholderLegTimeTicks = 78;

    public async Task<AssignShipmentToTruckResponse> HandleAsync(AssignShipmentToTruckRequest request, CancellationToken cancellationToken = default)
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

        if (truck.TruckType != shipment.RequiredTruckType)
        {
            throw new InvalidOperationException(
                $"This shipment requires a {shipment.RequiredTruckType} truck, but {truck.TruckName} is a {truck.TruckType}.");
        }

        if (truck.DriverAssignment is null)
        {
            throw new InvalidOperationException("Truck must have a driver assigned before it can accept a shipment.");
        }

        var company = await unitOfWork.TruckingCompanies.GetByIdAsync(truck.TruckingCompanyId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Trucking company '{truck.TruckingCompanyId.Value}' was not found.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var nonOfficeStopCount = truck.Stops.Count(stop => stop.Kind != Domain.Fleet.StopKind.Office);

        // Fresh GeoLocation/Capacity instances, not the tracked ones off Shipment/
        // TruckingCompany - EF Core's change tracker treats owned-type instances by
        // reference identity, so reusing shipment.PickupLocation etc. directly here
        // makes it conflate Stop's copy with Shipment's/TruckingCompany's owned
        // navigation of the same CLR type, corrupting insert/update detection for the
        // newly-created Stop rows (they get emitted as UPDATEs instead of INSERTs,
        // affecting 0 rows and throwing DbUpdateConcurrencyException).
        truck.AssignShipment(
            shipment.Id,
            Capacity.Create(shipment.Load.WeightKg, shipment.Load.VolumeCubicMeters),
            GeoLocation.Create(shipment.PickupLocation.Latitude, shipment.PickupLocation.Longitude),
            GeoLocation.Create(shipment.DeliveryLocation.Latitude, shipment.DeliveryLocation.Longitude),
            GeoLocation.Create(company.OfficeLocation.Latitude, company.OfficeLocation.Longitude),
            pickupInsertIndex: nonOfficeStopCount,
            deliveryInsertIndex: nonOfficeStopCount,
            pickupExpectedArrivalTime: now,
            deliveryExpectedArrivalTime: now);

        shipment.AssignToCompany(truck.TruckingCompanyId.Value);

        truck.DriverAssignment.PrimaryDriver.StartDriving(now);

        truck.StartLeg(PlaceholderLegDistanceKm, PlaceholderLegTimeTicks);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssignShipmentToTruckResponse(truck.Stops.Count);
    }
}
