using Freight.Domain.Common;
using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;

namespace Freight.Application.Fleet;

public sealed record CheckDriverEligibilityRequest(Guid DriverId, int AfterMinutes);

public sealed record CheckDriverEligibilityResponse(bool IsEligible, IneligibilityReason? Reason, int? MinutesUntilEligible);

/// <summary>
/// Read-only probe against a driver's compliance ledger: would they still be eligible to
/// drive <see cref="CheckDriverEligibilityRequest.AfterMinutes"/> minutes from now? Never
/// mutates the real ledger - <see cref="IDriverRuleEngine.IsEligibleToDriveFuture"/>
/// replays forward on a private clone.
/// </summary>
public sealed class CheckDriverEligibilityHandler(IUnitOfWork unitOfWork, IDriverRuleEngine driverRuleEngine)
{
    public async Task<CheckDriverEligibilityResponse> HandleAsync(CheckDriverEligibilityRequest request, CancellationToken cancellationToken = default)
    {
        if (request.AfterMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.AfterMinutes, "afterMinutes cannot be negative.");
        }

        var driver = await unitOfWork.Drivers.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new InvalidOperationException($"Driver '{request.DriverId}' was not found.");

        if (driver.ComplianceState is null)
        {
            throw new InvalidOperationException(
                $"Driver '{request.DriverId}' has never started driving - no compliance ledger exists yet.");
        }

        var eligibility = driverRuleEngine.IsEligibleToDriveFuture(
            driver.ComplianceState, driver.Rules, request.AfterMinutes, RestRuleLimits.Default);

        return new CheckDriverEligibilityResponse(eligibility.IsEligible, eligibility.Reason, eligibility.MinutesUntilEligible);
    }
}
