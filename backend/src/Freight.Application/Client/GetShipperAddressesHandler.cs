using Freight.Domain.Client;
using Freight.Domain.Common;

namespace Freight.Application.Client;

public sealed record GetShipperAddressesRequest(Guid ShipperId);

/// <param name="PlaceName">What a shipment booked from this address shows, e.g. "Main warehouse — 1100 Favoriten, Vienna".</param>
public sealed record ShipperAddressDto(
    Guid AddressId,
    string Label,
    string AreaName,
    string PlaceName,
    double Latitude,
    double Longitude,
    DateTime CreatedAt)
{
    public static ShipperAddressDto From(ShipperAddress address) => new(
        address.Id,
        address.Label,
        address.AreaName,
        address.PlaceName,
        address.Location.Latitude,
        address.Location.Longitude,
        address.CreatedAt);
}

public sealed record GetShipperAddressesResponse(IReadOnlyList<ShipperAddressDto> Addresses);

/// <summary>A shipper's saved addresses (their address book, ADR 0013), newest first.</summary>
public sealed class GetShipperAddressesHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetShipperAddressesResponse> GetShipperAddressesAsync(GetShipperAddressesRequest request, CancellationToken cancellationToken = default)
    {
        var shipper = await unitOfWork.Shippers.GetByIdAsync(request.ShipperId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipper '{request.ShipperId}' was not found.");

        return new GetShipperAddressesResponse(shipper.Addresses.Select(ShipperAddressDto.From).ToList());
    }
}
