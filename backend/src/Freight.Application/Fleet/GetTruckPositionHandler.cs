using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record GetTruckPositionRequest(Guid TruckId);

public sealed record TruckPositionDto(
    Guid TruckId,
    Guid? TripId,
    double Latitude,
    double Longitude,
    Guid? HeadingToStopId,
    double LegProgressFraction);

/// <summary>
/// Q1 - "where is my truck right now?" - see <see cref="TruckPositionLocator"/> for how the
/// position is worked out (office when no open trip, otherwise along the current leg).
/// </summary>
public sealed class GetTruckPositionHandler(IUnitOfWork unitOfWork)
{
    public async Task<TruckPositionDto> GetTruckPositionAsync(GetTruckPositionRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        if (truck.TruckingCompanyId is null)
        {
            throw new InvalidOperationException($"Truck '{truck.Id}' does not belong to a trucking company.");
        }

        var company = await unitOfWork.TruckingCompanies.GetByIdAsync(truck.TruckingCompanyId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Trucking company '{truck.TruckingCompanyId.Value}' was not found.");

        var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);
        var location = TruckPositionLocator.Locate(truck, trip, company.OfficeLocation);

        return new TruckPositionDto(
            truck.Id,
            trip?.Id,
            location.Position.Latitude,
            location.Position.Longitude,
            location.HeadingToStopId,
            location.LegProgressFraction);
    }
}
