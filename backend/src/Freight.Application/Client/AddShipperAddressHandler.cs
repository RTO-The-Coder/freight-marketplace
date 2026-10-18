using Freight.Domain.Common;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Client;

/// <param name="AreaName">The generic area of the geocoding match the shipper picked, e.g. "1100 Favoriten, Vienna".</param>
public sealed record AddShipperAddressRequest(Guid ShipperId, string Label, string AreaName, GeoLocation Location);

/// <summary>
/// Saves a new entry to a shipper's address book (ADR 0013). The area and coordinates come from
/// the "Find area" match the shipper picked (<c>GET /geocoding/search</c>), so no second
/// geocoding call is made here.
/// </summary>
public sealed class AddShipperAddressHandler(IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<ShipperAddressDto> AddShipperAddressAsync(AddShipperAddressRequest request, CancellationToken cancellationToken = default)
    {
        var shipper = await unitOfWork.Shippers.GetByIdAsync(request.ShipperId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipper '{request.ShipperId}' was not found.");

        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        var address = shipper.AddAddress(request.Label, request.AreaName, request.Location, clock.CurrentTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ShipperAddressDto.From(address);
    }
}
