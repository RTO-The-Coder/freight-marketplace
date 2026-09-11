using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tests.Fleet;

public class DriverAssignmentTests
{
    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: false);

    private static Driver SomeDriver() => Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules());

    [Fact]
    public void Single_ValidDriver_SetsPrimaryAndActiveDriver()
    {
        var driver = SomeDriver();

        var assignment = DriverAssignment.Single(driver);

        Assert.Equal(DriverConfigurationType.Single, assignment.ConfigurationType);
        Assert.Same(driver, assignment.PrimaryDriver);
        Assert.Null(assignment.SecondaryDriver);
        Assert.Equal(driver.Id, assignment.ActiveDriverId);
        Assert.Same(driver, assignment.ActiveDriver);
    }

    [Fact]
    public void Single_NullDriver_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DriverAssignment.Single(null!));
    }

    [Fact]
    public void Team_LargeTruck_SetsPrimaryAndSecondaryAndActivatesPrimary()
    {
        var first = SomeDriver();
        var second = SomeDriver();

        var assignment = DriverAssignment.Team(first, second, TruckSize.Large);

        Assert.Equal(DriverConfigurationType.Team, assignment.ConfigurationType);
        Assert.Same(first, assignment.PrimaryDriver);
        Assert.Same(second, assignment.SecondaryDriver);
        Assert.Equal(first.Id, assignment.ActiveDriverId);
    }

    [Fact]
    public void Team_SameDriverTwice_Throws()
    {
        var driver = SomeDriver();

        Assert.Throws<ArgumentException>(() => DriverAssignment.Team(driver, driver, TruckSize.Large));
    }

    [Fact]
    public void Team_SmallTruck_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => DriverAssignment.Team(SomeDriver(), SomeDriver(), TruckSize.Small));
    }

    [Fact]
    public void Team_MediumTruck_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => DriverAssignment.Team(SomeDriver(), SomeDriver(), TruckSize.Medium));
    }

    [Fact]
    public void Team_NullFirstDriver_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DriverAssignment.Team(null!, SomeDriver(), TruckSize.Large));
    }

    [Fact]
    public void Team_NullSecondDriver_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DriverAssignment.Team(SomeDriver(), null!, TruckSize.Large));
    }

    [Fact]
    public void AdvanceActiveDriver_ToNull_AlwaysAllowed()
    {
        var assignment = DriverAssignment.Single(SomeDriver());

        assignment.AdvanceActiveDriver(null);

        Assert.Null(assignment.ActiveDriverId);
        Assert.Null(assignment.ActiveDriver);
    }

    [Fact]
    public void AdvanceActiveDriver_FromNullToPrimary_Allowed()
    {
        var first = SomeDriver();
        var second = SomeDriver();
        var assignment = DriverAssignment.Team(first, second, TruckSize.Large);
        assignment.AdvanceActiveDriver(null);

        assignment.AdvanceActiveDriver(first.Id);

        Assert.Equal(first.Id, assignment.ActiveDriverId);
    }

    [Fact]
    public void AdvanceActiveDriver_FromNullToSecondary_Allowed()
    {
        var first = SomeDriver();
        var second = SomeDriver();
        var assignment = DriverAssignment.Team(first, second, TruckSize.Large);
        assignment.AdvanceActiveDriver(null);

        assignment.AdvanceActiveDriver(second.Id);

        Assert.Equal(second.Id, assignment.ActiveDriverId);
        Assert.Same(second, assignment.ActiveDriver);
    }

    [Fact]
    public void AdvanceActiveDriver_PrimaryToSecondary_Allowed()
    {
        var first = SomeDriver();
        var second = SomeDriver();
        var assignment = DriverAssignment.Team(first, second, TruckSize.Large);

        assignment.AdvanceActiveDriver(second.Id);

        Assert.Equal(second.Id, assignment.ActiveDriverId);
    }

    [Fact]
    public void AdvanceActiveDriver_SecondaryBackToPrimary_ThrowsEvenWithValidId()
    {
        var first = SomeDriver();
        var second = SomeDriver();
        var assignment = DriverAssignment.Team(first, second, TruckSize.Large);
        assignment.AdvanceActiveDriver(second.Id);

        Assert.Throws<InvalidOperationException>(() => assignment.AdvanceActiveDriver(first.Id));
    }

    [Fact]
    public void AdvanceActiveDriver_SecondaryToNullThenBackToSecondary_Allowed()
    {
        var first = SomeDriver();
        var second = SomeDriver();
        var assignment = DriverAssignment.Team(first, second, TruckSize.Large);
        assignment.AdvanceActiveDriver(second.Id);
        assignment.AdvanceActiveDriver(null);

        assignment.AdvanceActiveDriver(second.Id);

        Assert.Equal(second.Id, assignment.ActiveDriverId);
    }

    [Fact]
    public void AdvanceActiveDriver_SecondaryToNullThenPrimary_AllowedBecauseStickinessOnlyBlocksDirectMove()
    {
        // The one-directional guard compares against the CURRENT ActiveDriverId, so once the
        // pointer has been moved to null (stopped), a subsequent move to Primary is a
        // null -> Primary transition, not Secondary -> Primary - the guard does not look further
        // back in history than the immediately-preceding active driver.
        var first = SomeDriver();
        var second = SomeDriver();
        var assignment = DriverAssignment.Team(first, second, TruckSize.Large);
        assignment.AdvanceActiveDriver(second.Id);
        assignment.AdvanceActiveDriver(null);

        assignment.AdvanceActiveDriver(first.Id);

        Assert.Equal(first.Id, assignment.ActiveDriverId);
    }

    [Fact]
    public void AdvanceActiveDriver_UnknownDriverId_Throws()
    {
        var assignment = DriverAssignment.Single(SomeDriver());

        Assert.Throws<ArgumentException>(() => assignment.AdvanceActiveDriver(Guid.NewGuid()));
    }

    [Fact]
    public void AdvanceActiveDriver_AnotherAssignmentsDriverIdOnSingleAssignment_Throws()
    {
        var assignment = DriverAssignment.Single(SomeDriver());
        var unrelatedDriver = SomeDriver();

        Assert.Throws<ArgumentException>(() => assignment.AdvanceActiveDriver(unrelatedDriver.Id));
    }

    [Fact]
    public void HasDriverAbleToDrive_IsAlwaysTrue()
    {
        var assignment = DriverAssignment.Single(SomeDriver());

        Assert.True(assignment.HasDriverAbleToDrive);
    }
}
