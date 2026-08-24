using Freight.Domain.Common;
using Freight.Domain.Fleet;

namespace Freight.Application.Fleet;

public sealed record GetTruckDetailRequest(Guid TruckId);

public sealed record TruckDetailDriverDto(Guid DriverId, string FirstName, string LastName);

public sealed record TruckDetailStopDto(
    Guid StopId,
    Guid? ShipmentId,
    StopKind Kind,
    int Sequence,
    double Latitude,
    double Longitude,
    DateTime ExpectedArrivalTime);

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
    public async Task<TruckDetailDto> HandleAsync(GetTruckDetailRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        var assignment = truck.DriverAssignment;

        return new TruckDetailDto(
            truck.Id,
            truck.TruckName,
            truck.TruckType,
            truck.TruckSize,
            truck.IsActive,
            truck.Status,
            truck.TruckingCompanyId,
            assignment?.ConfigurationType,
            assignment is null ? null : ToDto(assignment.PrimaryDriver),
            assignment?.SecondaryDriver is null ? null : ToDto(assignment.SecondaryDriver),
            truck.Stops
                .Select(stop => new TruckDetailStopDto(
                    stop.Id,
                    stop.ShipmentId,
                    stop.Kind,
                    stop.Sequence,
                    stop.Location.Latitude,
                    stop.Location.Longitude,
                    stop.ExpectedArrivalTime))
                .ToList());
    }

    private static TruckDetailDriverDto ToDto(Driver driver) => new(driver.Id, driver.FirstName, driver.LastName);
}
