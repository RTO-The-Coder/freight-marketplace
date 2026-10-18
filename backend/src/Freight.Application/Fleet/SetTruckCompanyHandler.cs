using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record SetTruckCompanyRequest(Guid TruckId, Guid? TruckingCompanyId);

/// <summary>
/// Adds a truck to a company's fleet, or takes it out (with its drivers). A truck can't
/// move straight to another company, and can't leave its company while it has an open trip.
/// </summary>
public sealed class SetTruckCompanyHandler(IUnitOfWork unitOfWork)
{
    public async Task SetTruckCompanyAsync(SetTruckCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken) ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        if (request.TruckingCompanyId is { } companyId)
        {
            truck.AssignToCompany(companyId);
        }
        else
        {
            var openTrip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);
            if (openTrip is not null)
            {
                throw new InvalidOperationException(
                    $"Truck '{truck.Id}' has an open trip - it cannot leave its trucking company until the trip completes.");
            }

            truck.UnassignFromCompany();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
