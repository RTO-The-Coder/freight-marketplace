namespace Freight.Domain.Fleet;

public sealed class Driver
{
    public Guid Id { get; }
    public string FirstName { get; }
    public string LastName { get; }

    public Driver(Guid id, string firstName, string lastName)
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

        Id = id;
        FirstName = firstName;
        LastName = lastName;
    }
}
