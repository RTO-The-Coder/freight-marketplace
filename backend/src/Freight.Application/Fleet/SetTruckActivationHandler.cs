using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record SetTruckActivationRequest(Guid TruckId, bool IsActive);

public sealed class SetTruckActivationHandler(IUnitOfWork unitOfWork)
{
    public async Task HandleActivation(SetTruckActivationRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken) ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        if (request.IsActive)
            truck.Activate();
        else
            truck.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
