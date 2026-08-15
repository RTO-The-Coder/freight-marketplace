namespace Freight.Domain.Fleet;

public sealed class DriverAssignment
{
    public DriverConfigurationType ConfigurationType { get; }
    public Driver PrimaryDriver { get; }
    public Driver? SecondaryDriver { get; }

    private DriverAssignment(DriverConfigurationType configurationType, Driver primaryDriver, Driver? secondaryDriver)
    {
        ConfigurationType = configurationType;
        PrimaryDriver = primaryDriver;
        SecondaryDriver = secondaryDriver;
    }

    public static DriverAssignment Single(Driver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);

        return new DriverAssignment(DriverConfigurationType.Single, driver, secondaryDriver: null);
    }

    public static DriverAssignment Team(Driver first, Driver second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Id == second.Id)
        {
            throw new ArgumentException("A team assignment requires two distinct drivers.", nameof(second));
        }

        return new DriverAssignment(DriverConfigurationType.Team, first, second);
    }
}
