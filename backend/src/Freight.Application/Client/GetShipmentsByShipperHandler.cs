using Freight.Domain.Common;

namespace Freight.Application.Client;

public sealed record GetShipmentsByShipperRequest(Guid ShipperId);

public sealed record GetShipmentsByShipperResponse(IReadOnlyList<ShipmentSummaryDto> Shipments);

public sealed class GetShipmentsByShipperHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetShipmentsByShipperResponse> GetShipmentsByShipperAsync(GetShipmentsByShipperRequest request, CancellationToken cancellationToken = default)
    {
        var shipments = await unitOfWork.Shipments.GetByShipperIdAsync(request.ShipperId, cancellationToken);

        var dtos = shipments.Select(ShipmentSummaryDto.From).ToList();

        return new GetShipmentsByShipperResponse(dtos);
    }
}
