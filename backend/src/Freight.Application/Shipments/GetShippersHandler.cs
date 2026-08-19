using Freight.Domain.Common;

namespace Freight.Application.Shipments;

public sealed record ShipperSummaryDto(Guid ShipperId, string Name, string ContactEmail);

public sealed record GetShippersResponse(IReadOnlyList<ShipperSummaryDto> Shippers);

public sealed class GetShippersHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetShippersResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        var shippers = await unitOfWork.Shippers.GetAllAsync(cancellationToken);
        var dtos = shippers.Select(shipper => new ShipperSummaryDto(shipper.Id, shipper.Name, shipper.ContactEmail)).ToList();
        return new GetShippersResponse(dtos);
    }
}
