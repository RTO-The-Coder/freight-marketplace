using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tests.Fleet.Abstractions;

public class DriverProjectionTests
{
    private static readonly DateTime SimStart = new(2026, 1, 1, 6, 0, 0);

    private static DriverComplianceState Ledger(Guid? driverId = null) =>
        new(driverId ?? Guid.NewGuid(), SimStart);

    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: false);

    [Fact]
    public void Single_SetsPrimaryOnly_AndIsTeamFalse()
    {
        var ledger = Ledger();
        var rules = SomeRules();

        var projection = DriverProjection.Single(ledger, rules);

        Assert.Same(ledger, projection.PrimaryLedger);
        Assert.Same(rules, projection.PrimaryRules);
        Assert.Null(projection.SecondaryLedger);
        Assert.Null(projection.SecondaryRules);
        Assert.Null(projection.ActiveDriverId);
        Assert.False(projection.IsTeam);
    }

    [Fact]
    public void Team_SetsBothDriversAndActiveDriverId_AndIsTeamTrue()
    {
        var primaryId = Guid.NewGuid();
        var primaryLedger = Ledger(primaryId);
        var primaryRules = SomeRules();
        var secondaryLedger = Ledger();
        var secondaryRules = SomeRules();

        var projection = DriverProjection.Team(primaryLedger, primaryRules, secondaryLedger, secondaryRules, primaryId);

        Assert.Same(primaryLedger, projection.PrimaryLedger);
        Assert.Same(primaryRules, projection.PrimaryRules);
        Assert.Same(secondaryLedger, projection.SecondaryLedger);
        Assert.Same(secondaryRules, projection.SecondaryRules);
        Assert.Equal(primaryId, projection.ActiveDriverId);
        Assert.True(projection.IsTeam);
    }
}
