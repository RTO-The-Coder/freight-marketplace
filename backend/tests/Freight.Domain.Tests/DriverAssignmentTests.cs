using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tests;

public class DriverAssignmentTests
{
    private static DrivingRules Rules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: false);

    private static Driver NewDriver() => Driver.Create(Guid.NewGuid(), "John", "Doe", Rules());

    [Fact]
    public void Single_SetsConfigurationTypeAndPrimaryDriver()
    {
        var driver = NewDriver();

        var assignment = DriverAssignment.Single(driver);

        Assert.Equal(DriverConfigurationType.Single, assignment.ConfigurationType);
        Assert.Same(driver, assignment.PrimaryDriver);
        Assert.Null(assignment.SecondaryDriver);
    }

    [Fact]
    public void Team_OnLargeTruck_SetsConfigurationTypeAndBothDrivers()
    {
        var first = NewDriver();
        var second = NewDriver();

        var assignment = DriverAssignment.Team(first, second, TruckSize.Large);

        Assert.Equal(DriverConfigurationType.Team, assignment.ConfigurationType);
        Assert.Same(first, assignment.PrimaryDriver);
        Assert.Same(second, assignment.SecondaryDriver);
    }

    [Theory]
    [InlineData(TruckSize.Small)]
    [InlineData(TruckSize.Medium)]
    public void Team_OnNonLargeTruck_Throws(TruckSize size)
    {
        var first = NewDriver();
        var second = NewDriver();

        Assert.Throws<InvalidOperationException>(() => DriverAssignment.Team(first, second, size));
    }

    [Fact]
    public void Team_WithSameDriverTwice_Throws()
    {
        var driver = NewDriver();

        Assert.Throws<ArgumentException>(() => DriverAssignment.Team(driver, driver, TruckSize.Large));
    }

    [Fact]
    public void Single_NullDriver_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DriverAssignment.Single(null!));
    }

    [Fact]
    public void Team_NullSecondDriver_Throws()
    {
        var driver = NewDriver();

        Assert.Throws<ArgumentNullException>(() => DriverAssignment.Team(driver, null!, TruckSize.Large));
    }

    [Fact]
    public void AdvanceActiveDriver_NullToPrimary_Succeeds()
    {
        var primary = NewDriver();
        var assignment = DriverAssignment.Single(primary);

        assignment.AdvanceActiveDriver(primary.Id);

        Assert.Equal(primary.Id, assignment.ActiveDriverId);
    }

    [Fact]
    public void AdvanceActiveDriver_PrimaryToSecondary_Succeeds()
    {
        var primary = NewDriver();
        var secondary = NewDriver();
        var assignment = DriverAssignment.Team(primary, secondary, TruckSize.Large);
        assignment.AdvanceActiveDriver(primary.Id);

        assignment.AdvanceActiveDriver(secondary.Id);

        Assert.Equal(secondary.Id, assignment.ActiveDriverId);
    }

    [Fact]
    public void AdvanceActiveDriver_SecondaryBackToPrimary_Throws()
    {
        var primary = NewDriver();
        var secondary = NewDriver();
        var assignment = DriverAssignment.Team(primary, secondary, TruckSize.Large);
        assignment.AdvanceActiveDriver(primary.Id);
        assignment.AdvanceActiveDriver(secondary.Id);

        Assert.Throws<InvalidOperationException>(() => assignment.AdvanceActiveDriver(primary.Id));
        Assert.Equal(secondary.Id, assignment.ActiveDriverId);
    }

    [Fact]
    public void AdvanceActiveDriver_AnyStateToNull_Succeeds()
    {
        var primary = NewDriver();
        var secondary = NewDriver();
        var assignment = DriverAssignment.Team(primary, secondary, TruckSize.Large);
        assignment.AdvanceActiveDriver(primary.Id);
        assignment.AdvanceActiveDriver(secondary.Id);

        assignment.AdvanceActiveDriver(null);

        Assert.Null(assignment.ActiveDriverId);
    }

    [Fact]
    public void AdvanceActiveDriver_UnknownDriverId_Throws()
    {
        var primary = NewDriver();
        var assignment = DriverAssignment.Single(primary);

        Assert.Throws<ArgumentException>(() => assignment.AdvanceActiveDriver(Guid.NewGuid()));
    }
}
