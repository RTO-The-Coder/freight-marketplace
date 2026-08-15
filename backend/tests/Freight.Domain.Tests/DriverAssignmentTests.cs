using Freight.Domain.Fleet;

namespace Freight.Domain.Tests;

public class DriverAssignmentTests
{
    private static Driver NewDriver() => new(Guid.NewGuid(), "John", "Doe");

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
    public void Team_SetsConfigurationTypeAndBothDrivers()
    {
        var first = NewDriver();
        var second = NewDriver();

        var assignment = DriverAssignment.Team(first, second);

        Assert.Equal(DriverConfigurationType.Team, assignment.ConfigurationType);
        Assert.Same(first, assignment.PrimaryDriver);
        Assert.Same(second, assignment.SecondaryDriver);
    }

    [Fact]
    public void Team_WithSameDriverTwice_Throws()
    {
        var driver = NewDriver();

        Assert.Throws<ArgumentException>(() => DriverAssignment.Team(driver, driver));
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

        Assert.Throws<ArgumentNullException>(() => DriverAssignment.Team(driver, null!));
    }
}
