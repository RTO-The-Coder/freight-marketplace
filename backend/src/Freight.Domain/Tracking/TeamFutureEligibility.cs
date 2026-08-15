using Freight.Domain.Fleet;

namespace Freight.Domain.Tracking;

public sealed record TeamFutureEligibility(
    MovementState ResultingMovementState,
    Guid ActiveDriverId);
