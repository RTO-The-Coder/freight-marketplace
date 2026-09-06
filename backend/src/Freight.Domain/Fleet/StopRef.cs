namespace Freight.Domain.Fleet;

/// <summary>
/// Identifies a shipment stop within a trip by shipment + kind - unique per trip, and
/// unlike <see cref="Stop.Id"/> stable between a <see cref="Trip.Clone"/> preview and the
/// real trip (whose inserted stops get fresh ids). Carries the feasibility walk's per-stop
/// results back onto the real trip.
/// </summary>
public readonly record struct StopRef(Guid ShipmentId, StopKind Kind)
{
    /// <summary>Throws if <paramref name="stop"/> is an Office stop (no shipment).</summary>
    public static StopRef For(Stop stop)
    {
        if (stop.ShipmentId is not { } shipmentId)
        {
            throw new InvalidOperationException($"Stop '{stop.Id}' has no shipment - it cannot be referenced by StopRef.");
        }

        return new StopRef(shipmentId, stop.Kind);
    }
}
