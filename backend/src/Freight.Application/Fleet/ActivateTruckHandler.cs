using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record ActivateTruckRequest(Guid TruckId);

public sealed class ActivateTruckHandler(IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(ActivateTruckRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        truck.Activate();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
