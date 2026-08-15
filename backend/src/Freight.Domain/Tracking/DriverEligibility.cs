namespace Freight.Domain.Tracking;

public sealed record DriverEligibility(
    bool IsEligible,
    IneligibilityReason? Reason,
    int? MinutesUntilEligible);
