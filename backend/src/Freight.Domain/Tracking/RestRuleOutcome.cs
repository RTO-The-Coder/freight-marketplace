using Freight.Domain.Common;

namespace Freight.Domain.Tracking;

public sealed record RestRuleOutcome(
    DriverComplianceState UpdatedLedger,
    DriverActivity Action,
    IReadOnlyCollection<IDomainEvent> Events,
    bool WasPolicyOverridden);
