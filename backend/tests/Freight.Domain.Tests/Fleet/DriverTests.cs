using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tests.Fleet;

public class DriverTests
{
    private static readonly DateTime SimStart = new(2026, 1, 1, 6, 0, 0);

    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: false);

    [Fact]
    public void Create_WithExplicitId_SetsProperties()
    {
        var id = Guid.NewGuid();
        var rules = SomeRules();

        var driver = Driver.Create(id, "Jane", "Doe", rules);

        Assert.Equal(id, driver.Id);
        Assert.Equal("Jane", driver.FirstName);
        Assert.Equal("Doe", driver.LastName);
        Assert.Same(rules, driver.Rules);
        Assert.Null(driver.ComplianceState);
    }

    [Fact]
    public void Create_WithoutExplicitId_GeneratesNonEmptyId()
    {
        var driver = Driver.Create("Jane", "Doe", SomeRules());

        Assert.NotEqual(Guid.Empty, driver.Id);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Driver.Create(Guid.Empty, "Jane", "Doe", SomeRules()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankFirstName_Throws(string firstName)
    {
        Assert.Throws<ArgumentException>(() => Driver.Create(Guid.NewGuid(), firstName, "Doe", SomeRules()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankLastName_Throws(string lastName)
    {
        Assert.Throws<ArgumentException>(() => Driver.Create(Guid.NewGuid(), "Jane", lastName, SomeRules()));
    }

    [Fact]
    public void Create_NullRules_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Driver.Create(Guid.NewGuid(), "Jane", "Doe", null!));
    }

    [Fact]
    public void ResetComplianceForNewTrip_FirstTrip_CreatesFreshLedgerAnchoredAtStart()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules());

        driver.ResetComplianceForNewTrip(SimStart);

        Assert.NotNull(driver.ComplianceState);
        Assert.Equal(driver.Id, driver.ComplianceState!.DriverId);
        Assert.Equal(SimStart, driver.ComplianceState.LastEvaluatedSimulatedTime);
    }

    [Fact]
    public void ResetComplianceForNewTrip_ExistingLedger_DiscardsPriorAccumulatedMinutes()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules());
        driver.ResetComplianceForNewTrip(SimStart);
        driver.ComplianceState!.DailyDrivingMinutesToday = 300;
        driver.ComplianceState.WeeklyDrivingMinutesThisWeek = 1000;

        var newStart = SimStart.AddDays(1);
        driver.ResetComplianceForNewTrip(newStart);

        Assert.Equal(0, driver.ComplianceState.DailyDrivingMinutesToday);
        Assert.Equal(0, driver.ComplianceState.WeeklyDrivingMinutesThisWeek);
        Assert.Equal(newStart, driver.ComplianceState.LastEvaluatedSimulatedTime);
    }

    [Fact]
    public void ResetComplianceForNewTrip_ReplacesWithNewInstance()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules());
        driver.ResetComplianceForNewTrip(SimStart);
        var firstLedger = driver.ComplianceState;

        driver.ResetComplianceForNewTrip(SimStart.AddDays(1));

        Assert.NotSame(firstLedger, driver.ComplianceState);
    }
}
