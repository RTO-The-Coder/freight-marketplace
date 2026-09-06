using Freight.Domain.Tracking.Enums;

namespace Freight.Domain.Tracking.ValueObjects;

public sealed record TeamFutureEligibility(
    MovementState ResultingMovementState,
    Guid ActiveDriverId);
