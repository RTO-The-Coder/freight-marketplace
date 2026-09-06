namespace Freight.Domain.Fleet;

/// <summary>
/// One road leg's distance and time (whole 5-minute ticks), measured by the caller from
/// <c>IRoutingService</c>. <see cref="Trip.AssignShipment"/> writes these onto stops
/// rather than inventing figures - a mid-route insertion splits one hop into two
/// genuinely different road legs that only the caller can measure.
/// </summary>
public sealed record RouteSegment(double DistanceKm, int TimeTick);

/// <summary>
/// Every road leg <see cref="Trip.AssignShipment"/> writes for one shipment insertion.
/// The two follower legs implement the hop-splitting rule (a new stop takes over the
/// incoming leg of the stop it lands in front of) and are null only when that stop is
/// appended at the end of the pending non-office route.
/// </summary>
/// <param name="PickupIncoming">Predecessor -> pickup.</param>
/// <param name="PickupToFollower">Pickup -> the existing pending stop it displaced; null when the pickup is inserted last.</param>
/// <param name="DeliveryIncoming">Predecessor (possibly the just-inserted pickup) -> delivery.</param>
/// <param name="DeliveryToFollower">Delivery -> the pending stop it displaced; null when the delivery is inserted last.</param>
/// <param name="ToOffice">Last pending non-office stop -> the Office(return) stop; used only when that stop is created (first shipment on the trip).</param>
public sealed record LegPlan(
    RouteSegment PickupIncoming,
    RouteSegment? PickupToFollower,
    RouteSegment DeliveryIncoming,
    RouteSegment? DeliveryToFollower,
    RouteSegment ToOffice);
