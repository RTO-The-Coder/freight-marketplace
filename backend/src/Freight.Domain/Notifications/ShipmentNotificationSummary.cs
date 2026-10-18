using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Notifications;

/// <summary>
/// The light, no-eligibility-computed-yet payload sent when a Shipment is booked or its times
/// change (ADR 0007) - just enough for a dispatcher to judge relevance at a glance before
/// deciding whether to check their own fleet via the on-demand evaluate endpoint.
/// <see cref="IsDirect"/> marks a shipment booked straight to the notified company. The route is
/// named by the shipment's place names (ADR 0013), never its coordinates.
/// </summary>
public sealed record ShipmentNotificationSummary(
    Guid ShipmentId,
    string PickupPlaceName,
    string DeliveryPlaceName,
    TruckType RequiredTruckType,
    TimeWindow PickupWindow,
    bool IsDirect = false);
