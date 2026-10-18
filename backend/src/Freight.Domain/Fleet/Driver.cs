using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

public sealed class Driver
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public DrivingRules Rules { get; private set; } = null!;

    /// <summary>
    /// The company the driver works for. Set at creation; null after the driver leaves the
    /// company (<see cref="UnassignFromCompany"/>), until a company takes the driver on again.
    /// </summary>
    public Guid? TruckingCompanyId { get; private set; }

    /// <summary>
    /// This driver's driving-time compliance ledger. Null until the driver starts
    /// driving for the first time - see <see cref="StartDriving"/>.
    /// </summary>
    public DriverComplianceState? ComplianceState { get; private set; }

    // EF Core cannot bind Rules through the constructor below (it is an owned-type
    // navigation, and EF's constructor injection only binds scalar properties) - this
    // parameterless constructor exists solely so EF's materializer can construct an
    // instance and set the properties above via reflection. Create(...) remains the
    // only construction path reachable from application code.
    private Driver()
    {
    }

    private Driver(Guid id, string firstName, string lastName, DrivingRules rules, Guid truckingCompanyId)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Rules = rules;
        TruckingCompanyId = truckingCompanyId;
    }

    public static Driver Create(string firstName, string lastName, DrivingRules rules, Guid truckingCompanyId) =>
        Create(Guid.NewGuid(), firstName, lastName, rules, truckingCompanyId);

    public static Driver Create(Guid id, string firstName, string lastName, DrivingRules rules, Guid truckingCompanyId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Driver id cannot be empty.", nameof(id));
        }

        if (truckingCompanyId == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(truckingCompanyId));
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException("First name is required.", nameof(firstName));
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException("Last name is required.", nameof(lastName));
        }

        ArgumentNullException.ThrowIfNull(rules);

        return new Driver(id, firstName, lastName, rules, truckingCompanyId);
    }

    /// <summary>
    /// A company takes the driver on. A driver can't move straight from one company to
    /// another: it must leave its company first (<see cref="UnassignFromCompany"/>).
    /// </summary>
    public void AssignToCompany(Guid truckingCompanyId)
    {
        if (truckingCompanyId == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(truckingCompanyId));
        }

        if (TruckingCompanyId is { } current && current != truckingCompanyId)
        {
            throw new InvalidOperationException("This driver belongs to another trucking company. Remove it from that company first.");
        }

        TruckingCompanyId = truckingCompanyId;
    }

    /// <summary>
    /// The driver leaves its company. Taking the driver off its truck, and the open-trip
    /// guard, live in the handler, not here.
    /// </summary>
    public void UnassignFromCompany() => TruckingCompanyId = null;

    /// <summary>
    /// (Re)seeds this driver's compliance ledger for a trip that is opening now, anchored
    /// at <paramref name="tripStartedAt"/> and fully rested. Always replaces any existing
    /// ledger - Phase 1 treats every trip as starting from a clean, rested state (see the
    /// build plan's "per-driver, reset-on-trip-open" decision). Advanced forward from here
    /// by <see cref="Tracking.Abstractions.IDriverRuleEngine"/>.
    /// </summary>
    public void ResetComplianceForNewTrip(DateTime tripStartedAt)
    {
        ComplianceState = new DriverComplianceState(Id, tripStartedAt);
    }
}
