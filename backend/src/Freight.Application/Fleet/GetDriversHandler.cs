using Freight.Domain.Common;
using Freight.Domain.Fleet;

namespace Freight.Application.Fleet;

public sealed record GetDriversRequest(bool UnassignedOnly);

public sealed record DriverSummaryDto(Guid DriverId, string FirstName, string LastName);

public sealed record GetDriversResponse(IReadOnlyList<DriverSummaryDto> Drivers);

public sealed class GetDriversHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetDriversResponse> HandleAsync(GetDriversRequest request, CancellationToken cancellationToken = default)
    {
        var allDrivers = await unitOfWork.Drivers.GetAllAsync(cancellationToken);

        IEnumerable<Driver> filtered = allDrivers;

        if (request.UnassignedOnly)
        {
            var allTrucks = await unitOfWork.Trucks.GetAllAsync(cancellationToken);

            var assignedDriverIds = allTrucks
                .Where(truck => truck.DriverAssignment is not null)
                .SelectMany(truck => truck.DriverAssignment!.SecondaryDriver is null
                    ? [truck.DriverAssignment.PrimaryDriver.Id]
                    : (IEnumerable<Guid>)[truck.DriverAssignment.PrimaryDriver.Id, truck.DriverAssignment.SecondaryDriver.Id])
                .ToHashSet();

            filtered = filtered.Where(driver => !assignedDriverIds.Contains(driver.Id));
        }

        var dtos = filtered
            .Select(driver => new DriverSummaryDto(driver.Id, driver.FirstName, driver.LastName))
            .ToList();

        return new GetDriversResponse(dtos);
    }
}
