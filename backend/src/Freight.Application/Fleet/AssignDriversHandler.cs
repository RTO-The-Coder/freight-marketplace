using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record AssignDriversRequest(Guid TruckId, Guid PrimaryDriverId, Guid? SecondaryDriverId);

public sealed class AssignDriversHandler(IUnitOfWork unitOfWork)
{
    public async Task AssignDriversAsync(AssignDriversRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        var primaryDriver = await unitOfWork.Drivers.GetByIdAsync(request.PrimaryDriverId, cancellationToken)
            ?? throw new InvalidOperationException($"Driver '{request.PrimaryDriverId}' was not found.");

        var secondaryDriver = request.SecondaryDriverId is { } secondaryDriverId
            ? await unitOfWork.Drivers.GetByIdAsync(secondaryDriverId, cancellationToken)
                ?? throw new InvalidOperationException($"Driver '{secondaryDriverId}' was not found.")
            : null;

        truck.AssignDrivers(primaryDriver, secondaryDriver);

        // Drivers swapped in while a trip runs drove part of it - add them to the trip's record.
        var openTrip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);
        if (openTrip is not null)
        {
            openTrip.RecordDriver(primaryDriver);
            if (secondaryDriver is not null)
            {
                openTrip.RecordDriver(secondaryDriver);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
