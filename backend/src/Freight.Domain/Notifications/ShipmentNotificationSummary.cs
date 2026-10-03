using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Notifications;

/// <summary>
/// The light, no-eligibility-computed-yet payload sent to every TruckingCompany when a
/// Shipment is booked (ADR 0007) - just enough for a dispatcher to judge relevance at a
/// glance before deciding whether to check their own fleet via the on-demand evaluate
/// endpoint.
/// </summary>
public sealed record ShipmentNotificationSummary(
    Guid ShipmentId,
    GeoLocation PickupLocation,
    TruckType RequiredTruckType,
    TimeWindow PickupWindow);
