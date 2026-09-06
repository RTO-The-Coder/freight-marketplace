using Freight.Domain.Tracking.Enums;

namespace Freight.Domain.Tracking.ValueObjects;

public sealed record DriverEligibility(
    bool IsEligible,
    IneligibilityReason? Reason,
    int? MinutesUntilEligible);
