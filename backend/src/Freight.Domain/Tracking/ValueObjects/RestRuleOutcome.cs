using Freight.Domain.Common;
using Freight.Domain.Tracking.Enums;

namespace Freight.Domain.Tracking.ValueObjects;

public sealed record RestRuleOutcome(
    DriverComplianceState UpdatedLedger,
    DriverActivity Action,
    IReadOnlyCollection<IDomainEvent> Events,
    bool WasPolicyOverridden);
