using Freight.Domain.Common;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Shipments;

public sealed record UpdatePickupWindowRequest(Guid ShipmentId, TimeWindow NewPickupWindow);

public sealed class UpdatePickupWindowHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task HandleAsync(UpdatePickupWindowRequest request, CancellationToken cancellationToken = default)
    {
        var shipment = await unitOfWork.Shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment '{request.ShipmentId}' was not found.");

        shipment.UpdatePickupWindow(request.NewPickupWindow, timeProvider.GetUtcNow().UtcDateTime);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
