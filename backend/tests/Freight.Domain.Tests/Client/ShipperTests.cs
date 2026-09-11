using Freight.Domain.Client;

namespace Freight.Domain.Tests.Client;

public class ShipperTests
{
    [Fact]
    public void Create_ValidInput_SetsProperties()
    {
        var id = Guid.NewGuid();

        var shipper = Shipper.Create(id, "Acme Shipping", "contact@acme.com");

        Assert.Equal(id, shipper.Id);
        Assert.Equal("Acme Shipping", shipper.Name);
        Assert.Equal("contact@acme.com", shipper.ContactEmail);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Shipper.Create(Guid.Empty, "Acme Shipping", "contact@acme.com"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => Shipper.Create(Guid.NewGuid(), name, "contact@acme.com"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("acme.com")]
    public void Create_ContactEmailWithoutAtSign_Throws(string contactEmail)
    {
        Assert.Throws<ArgumentException>(() => Shipper.Create(Guid.NewGuid(), "Acme Shipping", contactEmail));
    }
}
