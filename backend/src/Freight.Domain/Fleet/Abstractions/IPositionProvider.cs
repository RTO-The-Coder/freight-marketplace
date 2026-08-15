using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet.Abstractions;

public interface IPositionProvider
{
    GeoCoordinate GetCurrentPosition(Truck truck);
}
