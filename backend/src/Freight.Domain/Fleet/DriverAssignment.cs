namespace Freight.Domain.Fleet;

public sealed class DriverAssignment
{
    public DriverConfigurationType ConfigurationType { get; private set; }
    public Driver PrimaryDriver { get; private set; } = null!;
    public Driver? SecondaryDriver { get; private set; }

    // EF Core cannot bind PrimaryDriver/SecondaryDriver through the constructor below
    // (they are reference navigations to the independent Driver entity, and EF's
    // constructor injection only binds scalar/owned properties) - this parameterless
    // constructor exists solely so EF's materializer can construct an instance and set
    // the properties above via reflection. Single(...)/Team(...) remain the only
    // construction path reachable from application code.
    private DriverAssignment()
    {
    }

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
