using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record AssignDriversRequest(Guid TruckId, Guid PrimaryDriverId, Guid? SecondaryDriverId);

public sealed class AssignDriversHandler(IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(AssignDriversRequest request, CancellationToken cancellationToken = default)
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

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
