using Freight.Domain.Fleet;

namespace Freight.Domain.Tests.Fleet;

public class DeviceTokenTests
{
    private static readonly DateTime RegisteredAt = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ValidInput_SetsProperties()
    {
        var id = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var deviceToken = DeviceToken.Create(id, companyId, "encrypted-fid", RegisteredAt);

        Assert.Equal(id, deviceToken.Id);
        Assert.Equal(companyId, deviceToken.TruckingCompanyId);
        Assert.Equal("encrypted-fid", deviceToken.Fid);
        Assert.Equal(RegisteredAt, deviceToken.RegisteredAt);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => DeviceToken.Create(Guid.Empty, Guid.NewGuid(), "fid", RegisteredAt));
    }

    [Fact]
    public void Create_EmptyCompanyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => DeviceToken.Create(Guid.NewGuid(), Guid.Empty, "fid", RegisteredAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankFid_Throws(string fid)
    {
        Assert.Throws<ArgumentException>(() => DeviceToken.Create(Guid.NewGuid(), Guid.NewGuid(), fid, RegisteredAt));
    }

    [Fact]
    public void ReplaceFid_ValidFid_ReplacesFidAndRegisteredAtButKeepsIdentity()
    {
        var deviceToken = DeviceToken.Create(Guid.NewGuid(), Guid.NewGuid(), "old-fid", RegisteredAt);
        var id = deviceToken.Id;
        var companyId = deviceToken.TruckingCompanyId;
        var later = RegisteredAt.AddDays(1);

        deviceToken.ReplaceFid("new-fid", later);

        Assert.Equal("new-fid", deviceToken.Fid);
        Assert.Equal(later, deviceToken.RegisteredAt);
        Assert.Equal(id, deviceToken.Id);
        Assert.Equal(companyId, deviceToken.TruckingCompanyId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReplaceFid_BlankFid_ThrowsAndKeepsOldFid(string fid)
    {
        var deviceToken = DeviceToken.Create(Guid.NewGuid(), Guid.NewGuid(), "old-fid", RegisteredAt);

        Assert.Throws<ArgumentException>(() => deviceToken.ReplaceFid(fid, RegisteredAt.AddDays(1)));
        Assert.Equal("old-fid", deviceToken.Fid);
        Assert.Equal(RegisteredAt, deviceToken.RegisteredAt);
    }
}
