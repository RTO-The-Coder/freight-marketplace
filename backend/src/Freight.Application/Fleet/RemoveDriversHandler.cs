using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record RemoveDriversRequest(Guid TruckId);

/// <summary>
/// Clears a truck's driver assignment (both slots) and, since a driverless truck cannot
/// be active, deactivates it. Rejected while the truck has an open trip - a truck that is
/// mid-journey cannot lose its driver.
/// </summary>
public sealed class RemoveDriversHandler(IUnitOfWork unitOfWork)
{
    public async Task RemoveDriversAsync(RemoveDriversRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        var openTrip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);
        if (openTrip is not null)
        {
            throw new InvalidOperationException(
                $"Truck '{truck.Id}' has an open trip - its drivers cannot be removed until the trip completes.");
        }

        truck.RemoveDrivers();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
