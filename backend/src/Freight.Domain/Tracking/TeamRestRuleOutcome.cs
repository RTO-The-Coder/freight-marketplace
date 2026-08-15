using Freight.Domain.Common;
using Freight.Domain.Fleet;

namespace Freight.Domain.Tracking;

public sealed record TeamRestRuleOutcome(
    DriverComplianceState UpdatedPrimaryLedger,
    DriverComplianceState UpdatedSecondaryLedger,
    Guid ActiveDriverId,
    MovementState ResultingMovementState,
    IReadOnlyCollection<IDomainEvent> Events,
    bool PrimaryWasPolicyOverridden,
    bool SecondaryWasPolicyOverridden);
