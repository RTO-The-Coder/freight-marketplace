namespace Freight.Domain.Shipment;

public sealed class Shipper
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string ContactEmail { get; private set; } = null!;

    // EF Core's materializer constructs instances via this parameterless constructor
    // and sets the properties above via reflection - see the same pattern on
    // TruckingCompany. Create(...) remains the only construction path reachable from
    // application code.
    private Shipper()
    {
    }

    private Shipper(Guid id, string name, string contactEmail)
    {
        Id = id;
        Name = name;
        ContactEmail = contactEmail;
    }

    public static Shipper Create(Guid id, string name, string contactEmail)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Shipper id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Shipper name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(contactEmail) || !contactEmail.Contains('@'))
        {
            throw new ArgumentException("Shipper contact email must be a valid email address.", nameof(contactEmail));
        }

        return new Shipper(id, name, contactEmail);
    }
}
