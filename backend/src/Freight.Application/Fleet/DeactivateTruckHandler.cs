using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record DeactivateTruckRequest(Guid TruckId);

public sealed class DeactivateTruckHandler(IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(DeactivateTruckRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        truck.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
