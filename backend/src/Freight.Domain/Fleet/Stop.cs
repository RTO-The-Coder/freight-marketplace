namespace Freight.Domain.Fleet;

public sealed record Stop(Guid ShipmentId, StopKind Kind);
