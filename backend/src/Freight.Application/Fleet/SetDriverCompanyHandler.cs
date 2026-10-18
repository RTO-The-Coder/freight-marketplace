using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record SetDriverCompanyRequest(Guid DriverId, Guid? TruckingCompanyId);

/// <summary>
/// A company takes a driver on, or the driver leaves its company. A driver can't move
/// straight to another company. Leaving is refused while the driver's truck has an open
/// trip; otherwise the driver comes off its truck (a team's other driver stays on alone).
/// </summary>
public sealed class SetDriverCompanyHandler(IUnitOfWork unitOfWork)
{
    public async Task SetDriverCompanyAsync(SetDriverCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new InvalidOperationException($"Driver '{request.DriverId}' was not found.");

        if (request.TruckingCompanyId is { } companyId)
        {
            _ = await unitOfWork.TruckingCompanies.GetByIdAsync(companyId, cancellationToken)
                ?? throw new InvalidOperationException($"Trucking company '{companyId}' was not found.");

            driver.AssignToCompany(companyId);
        }
        else
        {
            var trucks = await unitOfWork.Trucks.GetAllAsync(cancellationToken);
            var truck = trucks.FirstOrDefault(t =>
                t.DriverAssignment is not null
                && (t.DriverAssignment.PrimaryDriver.Id == driver.Id
                    || t.DriverAssignment.SecondaryDriver?.Id == driver.Id));

            if (truck is not null)
            {
                var openTrip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);
                if (openTrip is not null)
                {
                    throw new InvalidOperationException(
                        $"Driver '{driver.Id}' is on an open trip - it cannot leave its trucking company until the trip completes.");
                }

                truck.RemoveDriver(driver.Id);
            }

            driver.UnassignFromCompany();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
