using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet.Abstractions;

public interface IPositionProvider
{
    GeoLocation GetCurrentPosition(Truck truck);
}
