using Freight.Domain.Common;
using Freight.Domain.Client.Enums;

namespace Freight.Application.Client;

public sealed record GetPendingShipmentsResponse(IReadOnlyList<ShipmentSummaryDto> Shipments);

public sealed class GetPendingShipmentsHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<GetPendingShipmentsResponse> GetPendingShipmentsAsync(CancellationToken cancellationToken = default)
    {
        var shipments = await unitOfWork.Shipments.GetByStatusAsync(ShipmentStatus.Pending, cancellationToken);

        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        var dtos = shipments.Select(shipment => ShipmentSummaryDto.From(shipment, clock.CurrentTime)).ToList();

        return new GetPendingShipmentsResponse(dtos);
    }
}
