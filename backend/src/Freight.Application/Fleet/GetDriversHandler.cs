using Freight.Domain.Common;
using Freight.Domain.Fleet;

namespace Freight.Application.Fleet;

public sealed record GetDriversRequest(bool UnassignedOnly, Guid? TruckingCompanyId = null);

public sealed record DriverSummaryDto(Guid DriverId, string FirstName, string LastName);

public sealed record GetDriversResponse(IReadOnlyList<DriverSummaryDto> Drivers);

public sealed class GetDriversHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetDriversResponse> GetDriversAsync(GetDriversRequest request, CancellationToken cancellationToken = default)
    {
        var drivers = request.TruckingCompanyId is { } companyId
            ? await unitOfWork.Drivers.GetByTruckingCompanyIdAsync(companyId, cancellationToken)
            : await unitOfWork.Drivers.GetAllAsync(cancellationToken);

        IEnumerable<Driver> filtered = drivers;

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
