using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Notifications;

/// <summary>
/// The light, no-eligibility-computed-yet payload sent when a Shipment is booked or its times
/// change (ADR 0007) - just enough for a dispatcher to judge relevance at a glance before
/// deciding whether to check their own fleet via the on-demand evaluate endpoint.
/// <see cref="IsDirect"/> marks a shipment booked straight to the notified company.
/// </summary>
public sealed record ShipmentNotificationSummary(
    Guid ShipmentId,
    GeoLocation PickupLocation,
    TruckType RequiredTruckType,
    TimeWindow PickupWindow,
    bool IsDirect = false);
