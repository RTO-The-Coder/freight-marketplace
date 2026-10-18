using Freight.Domain.Client;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests.Client;

public class ShipperAddressTests
{
    private static readonly DateTime Now = new(2026, 8, 1, 5, 0, 0);
    private static readonly GeoLocation Favoriten = GeoLocation.Create(48.1550, 16.3820);

    private static Shipper SomeShipper() => Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.com");

    [Fact]
    public void AddAddress_ValidInput_SavesTrimmedAddressWithItsPlaceName()
    {
        var shipper = SomeShipper();

        var address = shipper.AddAddress("  Main warehouse ", " 1100 Favoriten, Vienna ", Favoriten, Now);

        Assert.NotEqual(Guid.Empty, address.Id);
        Assert.Equal("Main warehouse", address.Label);
        Assert.Equal("1100 Favoriten, Vienna", address.AreaName);
        Assert.Equal(Favoriten, address.Location);
        Assert.Equal(Now, address.CreatedAt);
        Assert.Equal("Main warehouse — 1100 Favoriten, Vienna", address.PlaceName);
        Assert.Same(address, Assert.Single(shipper.Addresses));
    }

    [Fact]
    public void Addresses_AreSortedByNameThenArea_NotByWhenSaved()
    {
        var shipper = SomeShipper();
        var warehouse = shipper.AddAddress("Warehouse", "1100 Favoriten, Vienna", Favoriten, Now);
        var depotVienna = shipper.AddAddress("depot", "1100 Favoriten, Vienna", Favoriten, Now.AddHours(1));
        var depotMunich = shipper.AddAddress("Depot", "80803 Schwabing, Munich", Favoriten, Now.AddHours(2));

        Assert.Equal([depotVienna, depotMunich, warehouse], shipper.Addresses);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddAddress_BlankLabel_Throws(string label)
    {
        Assert.Throws<ArgumentException>(() => SomeShipper().AddAddress(label, "1100 Favoriten, Vienna", Favoriten, Now));
    }

    [Fact]
    public void AddAddress_LabelTooLong_Throws()
    {
        var label = new string('x', ShipperAddress.MaxLabelLength + 1);

        Assert.Throws<ArgumentException>(() => SomeShipper().AddAddress(label, "1100 Favoriten, Vienna", Favoriten, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddAddress_BlankArea_Throws(string areaName)
    {
        Assert.Throws<ArgumentException>(() => SomeShipper().AddAddress("Warehouse", areaName, Favoriten, Now));
    }

    [Fact]
    public void AddAddress_AreaTooLong_Throws()
    {
        var areaName = new string('x', ShipperAddress.MaxAreaNameLength + 1);

        Assert.Throws<ArgumentException>(() => SomeShipper().AddAddress("Warehouse", areaName, Favoriten, Now));
    }

    [Fact]
    public void AddAddress_NullLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SomeShipper().AddAddress("Warehouse", "1100 Favoriten, Vienna", null!, Now));
    }

    [Fact]
    public void AddAddress_Invalid_LeavesAddressBookUnchanged()
    {
        var shipper = SomeShipper();

        Assert.Throws<ArgumentException>(() => shipper.AddAddress("", "1100 Favoriten, Vienna", Favoriten, Now));

        Assert.Empty(shipper.Addresses);
    }

    [Theory]
    [InlineData("1100", "Favoriten", "Vienna", "1100 Favoriten, Vienna")]
    [InlineData("  120 00 ", "  Vinohrady ", " Prague ", "120 00 Vinohrady, Prague")]
    [InlineData("1100", "Favoriten", "", "1100 Favoriten")]
    [InlineData("", "Favoriten", "Vienna", "Favoriten, Vienna")]
    [InlineData("1100", "", "Vienna", "1100, Vienna")]
    [InlineData("10178", "Berlin", "berlin", "10178, berlin")]
    [InlineData(null, null, "Vienna", "Vienna")]
    public void ComposeAreaName_JoinsPostcodeSuburbAndCity_LeavingOutMissingParts(
        string? postcode, string? suburb, string? city, string expected)
    {
        Assert.Equal(expected, ShipperAddress.ComposeAreaName(postcode, suburb, city));
    }
}
