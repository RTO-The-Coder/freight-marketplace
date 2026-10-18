using Freight.Domain.ValueObjects;

namespace Freight.Domain.Client;

public sealed class Shipper
{
    private readonly List<ShipperAddress> _addresses = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string ContactEmail { get; private set; } = null!;

    /// <summary>The shipper's saved addresses, newest first. Add-only (<see cref="AddAddress"/>).</summary>
    public IReadOnlyList<ShipperAddress> Addresses => [.. _addresses.OrderByDescending(address => address.CreatedAt)];

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

    /// <summary>
    /// Saves a generic area to the address book under the shipper's own <paramref name="label"/>
    /// - the area and coordinates come from a geocoding match the shipper picked (ADR 0013).
    /// </summary>
    public ShipperAddress AddAddress(string label, string areaName, GeoLocation location, DateTime createdAt)
    {
        var address = ShipperAddress.Create(label, areaName, location, createdAt);
        _addresses.Add(address);
        return address;
    }
}
