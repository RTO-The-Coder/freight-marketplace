using Freight.Domain.Common;
using Freight.Domain.Fleet;

namespace Freight.Application.Fleet;

public sealed record GetFleetTreeRequest(Guid TruckingCompanyId);

public sealed record FleetDriverDto(Guid DriverId, string FirstName, string LastName);

public sealed record FleetDriverAssignmentDto(
    DriverConfigurationType ConfigurationType,
    FleetDriverDto PrimaryDriver,
    FleetDriverDto? SecondaryDriver,
    Guid? ActiveDriverId);

public sealed record FleetTruckDto(
    Guid TruckId,
    string TruckName,
    TruckType TruckType,
    TruckSize TruckSize,
    bool IsActive,
    TruckStatus Status,
    FleetDriverAssignmentDto? DriverAssignment);

public sealed record GetFleetTreeResponse(
    IReadOnlyList<FleetTruckDto> Trucks,
    IReadOnlyList<FleetDriverDto> UnassignedDrivers);

public sealed class GetFleetTreeHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetFleetTreeResponse> HandleAsync(GetFleetTreeRequest request, CancellationToken cancellationToken = default)
    {
        var trucks = await unitOfWork.Trucks.GetByTruckingCompanyIdAsync(request.TruckingCompanyId, cancellationToken);
        var allDrivers = await unitOfWork.Drivers.GetAllAsync(cancellationToken);

        var assignedDriverIds = trucks
            .Where(truck => truck.DriverAssignment is not null)
            .SelectMany(truck => truck.DriverAssignment!.SecondaryDriver is null
                ? [truck.DriverAssignment.PrimaryDriver.Id]
                : (IEnumerable<Guid>)[truck.DriverAssignment.PrimaryDriver.Id, truck.DriverAssignment.SecondaryDriver.Id])
            .ToHashSet();

        var truckDtos = new List<FleetTruckDto>();

        foreach (var truck in trucks)
        {
            var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);

            truckDtos.Add(new FleetTruckDto(
                truck.Id,
                truck.TruckName,
                truck.TruckType,
                truck.TruckSize,
                truck.IsActive,
                truck.DetermineStatus(trip),
                truck.DriverAssignment is null
                    ? null
                    : new FleetDriverAssignmentDto(
                        truck.DriverAssignment.ConfigurationType,
                        ToDto(truck.DriverAssignment.PrimaryDriver),
                        truck.DriverAssignment.SecondaryDriver is null ? null : ToDto(truck.DriverAssignment.SecondaryDriver),
                        truck.DriverAssignment.ActiveDriverId)));
        }

        var unassignedDrivers = allDrivers
            .Where(driver => !assignedDriverIds.Contains(driver.Id))
            .Select(ToDto)
            .ToList();

        return new GetFleetTreeResponse(truckDtos, unassignedDrivers);
    }

    private static FleetDriverDto ToDto(Driver driver) => new(driver.Id, driver.FirstName, driver.LastName);
}
