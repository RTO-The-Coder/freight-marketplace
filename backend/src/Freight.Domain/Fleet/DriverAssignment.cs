namespace Freight.Domain.Fleet;

public sealed class DriverAssignment
{
    public DriverConfigurationType ConfigurationType { get; private set; }
    public Driver PrimaryDriver { get; private set; } = null!;
    public Driver? SecondaryDriver { get; private set; }

    /// <summary>
    /// Which driver is currently at the wheel, if any. Sticky and one-directional - see
    /// <see cref="AdvanceActiveDriver"/>.
    /// </summary>
    public Guid? ActiveDriverId { get; private set; }

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
        ActiveDriverId = null;
    }

    public static DriverAssignment Single(Driver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);

        var assignment = new DriverAssignment(DriverConfigurationType.Single, driver, secondaryDriver: null);
        assignment.ActiveDriverId = driver.Id;
        return assignment;
    }

    /// <summary>
    /// A two-driver assignment. Only <see cref="TruckSize.Large"/> trucks may carry a
    /// second driver.
    /// </summary>
    public static DriverAssignment Team(Driver first, Driver second, TruckSize truckSize)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Id == second.Id)
        {
            throw new ArgumentException("A team assignment requires two distinct drivers.", nameof(second));
        }

        if (truckSize != TruckSize.Large)
        {
            throw new InvalidOperationException("Only Large trucks may be assigned a second driver.");
        }

        var assignment = new DriverAssignment(DriverConfigurationType.Team, first, second);
        assignment.ActiveDriverId = first.Id;
        return assignment;
    }

    /// <summary>
    /// Moves the active-driver pointer to <paramref name="candidateDriverId"/>, enforcing
    /// the one-directional stickiness invariant: null -> Primary -> Secondary -> null.
    /// Moving to null (stopping) is always allowed from any state, and a stopped truck
    /// may start on either driver - but once the secondary is active, the assignment
    /// never falls back to the primary, even if the primary later recovers.
    /// </summary>
    public void AdvanceActiveDriver(Guid? candidateDriverId)
    {
        if (candidateDriverId is { } candidate
            && candidate != PrimaryDriver.Id
            && candidate != SecondaryDriver?.Id)
        {
            throw new ArgumentException(
                "The active driver must be the primary or secondary driver of this assignment.",
                nameof(candidateDriverId));
        }

        var isBackwardMove =
            SecondaryDriver is not null
            && ActiveDriverId == SecondaryDriver.Id
            && candidateDriverId == PrimaryDriver.Id;

        if (isBackwardMove)
        {
            throw new InvalidOperationException(
                "The active driver moves one-directionally - it cannot return to the primary driver once the secondary driver is active.");
        }

        ActiveDriverId = candidateDriverId;
    }

    /// <summary>The <see cref="Driver"/> currently at the wheel, if any.</summary>
    public Driver? ActiveDriver =>
        ActiveDriverId is null
            ? null
            : ActiveDriverId == PrimaryDriver.Id
                ? PrimaryDriver
                : SecondaryDriver;

    /// <summary>
    /// Whether any driver in this assignment could drive right now. This slice does not
    /// track driving-time/compliance state (see the domain reconciliation plan), so any
    /// assigned driver is assumed able to drive.
    /// </summary>
    public bool HasDriverAbleToDrive => true;
}
