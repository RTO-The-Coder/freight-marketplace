using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;

namespace Freight.Application.Fleet;

public sealed record GetTruckDetailRequest(Guid TruckId);

public sealed record TruckDetailDriverDto(Guid DriverId, string FirstName, string LastName);

/// <param name="PlaceName">
/// Derived when read, never stored on the stop: a Pickup/Delivery stop shows its shipment's
/// place name, the Office stop the company's office place name.
/// </param>
public sealed record TruckDetailStopDto(
    Guid StopId,
    Guid? ShipmentId,
    StopKind Kind,
    StopStatus Status,
    int Sequence,
    double Latitude,
    double Longitude,
    string PlaceName,
    double IncomingLegDistanceKm,
    int IncomingLegTimeTick,
    DateTime? ReachedAt);

public sealed record TruckDetailDto(
    Guid TruckId,
    string TruckName,
    TruckType TruckType,
    TruckSize TruckSize,
    bool IsActive,
    TruckStatus Status,
    Guid? TruckingCompanyId,
    DriverConfigurationType? DriverConfigurationType,
    TruckDetailDriverDto? PrimaryDriver,
    TruckDetailDriverDto? SecondaryDriver,
    IReadOnlyList<TruckDetailStopDto> Stops);

public sealed class GetTruckDetailHandler(IUnitOfWork unitOfWork)
{
    public async Task<TruckDetailDto> GetTruckDetailAsync(GetTruckDetailRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);

        var assignment = truck.DriverAssignment;
        var placeNames = trip is null ? new Dictionary<Guid, string>() : await StopPlaceNamesAsync(trip, cancellationToken);

        return new TruckDetailDto(
            truck.Id,
            truck.TruckName,
            truck.Type,
            truck.Size,
            truck.IsActive,
            truck.DetermineStatus(trip),
            truck.TruckingCompanyId,
            assignment?.ConfigurationType,
            assignment is null ? null : ToDto(assignment.PrimaryDriver),
            assignment?.SecondaryDriver is null ? null : ToDto(assignment.SecondaryDriver),
            trip is null
                ? []
                : trip.Stops
                    .Select(stop => new TruckDetailStopDto(
                        stop.Id,
                        stop.ShipmentId,
                        stop.Kind,
                        stop.Status,
                        stop.Sequence,
                        stop.Location.Latitude,
                        stop.Location.Longitude,
                        placeNames.GetValueOrDefault(stop.Id, string.Empty),
                        stop.IncomingLegDistanceKm,
                        stop.IncomingLegTimeTick,
                        stop.ReachedAt))
                    .ToList());
    }

    /// <summary>Each stop's display name by stop id - one query for the trip's shipments, one for the office's company.</summary>
    private async Task<Dictionary<Guid, string>> StopPlaceNamesAsync(Trip trip, CancellationToken cancellationToken)
    {
        var shipmentIds = trip.Stops
            .Where(stop => stop.ShipmentId is not null)
            .Select(stop => stop.ShipmentId!.Value)
            .Distinct()
            .ToList();
        var shipments = (await unitOfWork.Shipments.GetByIdsAsync(shipmentIds, cancellationToken))
            .ToDictionary(shipment => shipment.Id);

        var company = await unitOfWork.TruckingCompanies.GetByIdAsync(trip.TruckingCompanyId, cancellationToken);

        var names = new Dictionary<Guid, string>();
        foreach (var stop in trip.Stops)
        {
            var name = stop.Kind switch
            {
                StopKind.Pickup when stop.ShipmentId is { } id && shipments.TryGetValue(id, out var shipment) => shipment.PickupPlaceName,
                StopKind.Delivery when stop.ShipmentId is { } id && shipments.TryGetValue(id, out var shipment) => shipment.DeliveryPlaceName,
                StopKind.Office => company?.OfficePlaceName,
                _ => null,
            };
            names[stop.Id] = name ?? string.Empty;
        }

        return names;
    }

    private static TruckDetailDriverDto ToDto(Driver driver) => new(driver.Id, driver.FirstName, driver.LastName);
}
