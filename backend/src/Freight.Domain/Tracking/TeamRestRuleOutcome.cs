using Freight.Domain.Common;

namespace Freight.Domain.Tracking;

public sealed record TeamRestRuleOutcome(
    DriverComplianceState UpdatedPrimaryLedger,
    DriverComplianceState UpdatedSecondaryLedger,
    Guid ActiveDriverId,
    MovementState ResultingMovementState,
    IReadOnlyCollection<IDomainEvent> Events,
    bool PrimaryWasPolicyOverridden,
    bool SecondaryWasPolicyOverridden);
