namespace Freight.Domain.Shipment;

public sealed class Shipper
{
    public Guid Id { get; }
    public string Name { get; }
    public string ContactEmail { get; }

    public Shipper(Guid id, string name, string contactEmail)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Shipper id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Shipper name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            throw new ArgumentException("Shipper contact email is required.", nameof(contactEmail));
        }

        Id = id;
        Name = name;
        ContactEmail = contactEmail;
    }
}
