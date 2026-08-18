using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

public sealed class Driver
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public DrivingRules Rules { get; private set; } = null!;

    // EF Core cannot bind Rules through the constructor below (it is an owned-type
    // navigation, and EF's constructor injection only binds scalar properties) - this
    // parameterless constructor exists solely so EF's materializer can construct an
    // instance and set the properties above via reflection. Create(...) remains the
    // only construction path reachable from application code.
    private Driver()
    {
    }

    private Driver(Guid id, string firstName, string lastName, DrivingRules rules)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Rules = rules;
    }

    public static Driver Create(string firstName, string lastName, DrivingRules rules) =>
        Create(Guid.NewGuid(), firstName, lastName, rules);

    public static Driver Create(Guid id, string firstName, string lastName, DrivingRules rules)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Driver id cannot be empty.", nameof(id));
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

        return new Driver(id, firstName, lastName, rules);
    }
}
