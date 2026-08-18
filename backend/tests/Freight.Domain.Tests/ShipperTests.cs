using Freight.Domain.Shipment;

namespace Freight.Domain.Tests;

public class ShipperTests
{
    [Fact]
    public void Create_ValidInput_SetsProperties()
    {
        var id = Guid.NewGuid();

        var shipper = Shipper.Create(id, "Acme Shipping", "contact@acme.example");

        Assert.Equal(id, shipper.Id);
        Assert.Equal("Acme Shipping", shipper.Name);
        Assert.Equal("contact@acme.example", shipper.ContactEmail);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Shipper.Create(Guid.Empty, "Acme Shipping", "contact@acme.example"));
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => Shipper.Create(Guid.NewGuid(), "", "contact@acme.example"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Create_InvalidContactEmail_Throws(string contactEmail)
    {
        Assert.Throws<ArgumentException>(() => Shipper.Create(Guid.NewGuid(), "Acme Shipping", contactEmail));
    }
}
