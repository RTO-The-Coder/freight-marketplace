using Freight.Domain.Common;
using Freight.Domain.Client.Enums;

namespace Freight.Application.Client;

public sealed record GetPendingShipmentsResponse(IReadOnlyList<ShipmentSummaryDto> Shipments);

public sealed class GetPendingShipmentsHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetPendingShipmentsResponse> GetPendingShipmentsAsync(CancellationToken cancellationToken = default)
    {
        var shipments = await unitOfWork.Shipments.GetByStatusAsync(ShipmentStatus.Pending, cancellationToken);

        var dtos = shipments.Select(ShipmentSummaryDto.From).ToList();

        return new GetPendingShipmentsResponse(dtos);
    }
}
