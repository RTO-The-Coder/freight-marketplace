using Freight.Domain.Common;
using Freight.Domain.Fleet.Enums;

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
/// Q1 - "where is my truck right now?". A truck with no open trip sits at its trucking
/// company's office. A truck on an open trip is somewhere along the leg toward its next
/// pending stop: the point <see cref="RouteProgress.GetProgressFraction"/> of the way
/// from the last stop it reached (or the office, if it has not reached one yet) to that
/// next stop. Straight-line interpolation - the route model assumes distance covered
/// tracks time elapsed (see <see cref="RouteProgress"/>); no OSRM call.
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

        var office = company.OfficeLocation;

        var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);
        var nextStop = trip?.NextStop;

        if (trip is null || nextStop is null || truck.CurrentProgress is null)
        {
            return new TruckPositionDto(truck.Id, trip?.Id, office.Latitude, office.Longitude, null, 0);
        }

        var legStart = trip.Stops.LastOrDefault(stop => stop.Status == StopStatus.Reached)?.Location ?? office;
        var fraction = truck.CurrentProgress.GetProgressFraction();
        var position = legStart.InterpolateTo(nextStop.Location, fraction);

        return new TruckPositionDto(
            truck.Id, trip.Id, position.Latitude, position.Longitude, nextStop.Id, fraction);
    }
}
