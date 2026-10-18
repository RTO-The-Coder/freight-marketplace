using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;

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

/// <summary>One of the company's drivers, with the truck it is assigned to (if any).</summary>
public sealed record FleetCompanyDriverDto(
    Guid DriverId,
    string FirstName,
    string LastName,
    Guid? AssignedTruckId,
    string? AssignedTruckName);

public sealed record GetFleetTreeResponse(
    IReadOnlyList<FleetTruckDto> Trucks,
    IReadOnlyList<FleetDriverDto> UnassignedDrivers,
    IReadOnlyList<FleetCompanyDriverDto> Drivers);

public sealed class GetFleetTreeHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetFleetTreeResponse> HandleAsync(GetFleetTreeRequest request, CancellationToken cancellationToken = default)
    {
        var trucks = await unitOfWork.Trucks.GetByTruckingCompanyIdAsync(request.TruckingCompanyId, cancellationToken);
        var companyDrivers = await unitOfWork.Drivers.GetByTruckingCompanyIdAsync(request.TruckingCompanyId, cancellationToken);

        // driverId -> the company truck it is assigned to.
        var truckByDriverId = trucks
            .Where(truck => truck.DriverAssignment is not null)
            .SelectMany(truck => truck.DriverAssignment!.SecondaryDriver is null
                ? [(DriverId: truck.DriverAssignment.PrimaryDriver.Id, Truck: truck)]
                : (IEnumerable<(Guid DriverId, Truck Truck)>)
                [
                    (truck.DriverAssignment.PrimaryDriver.Id, truck),
                    (truck.DriverAssignment.SecondaryDriver.Id, truck)
                ])
            .ToDictionary(pair => pair.DriverId, pair => pair.Truck);

        var truckDtos = new List<FleetTruckDto>();

        foreach (var truck in trucks)
        {
            var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);

            truckDtos.Add(new FleetTruckDto(
                truck.Id,
                truck.TruckName,
                truck.Type,
                truck.Size,
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

        var unassignedDrivers = companyDrivers
            .Where(driver => !truckByDriverId.ContainsKey(driver.Id))
            .Select(ToDto)
            .ToList();

        var drivers = companyDrivers
            .Select(driver =>
            {
                var truck = truckByDriverId.GetValueOrDefault(driver.Id);
                return new FleetCompanyDriverDto(driver.Id, driver.FirstName, driver.LastName, truck?.Id, truck?.TruckName);
            })
            .ToList();

        return new GetFleetTreeResponse(truckDtos, unassignedDrivers, drivers);
    }

    private static FleetDriverDto ToDto(Driver driver) => new(driver.Id, driver.FirstName, driver.LastName);
}
