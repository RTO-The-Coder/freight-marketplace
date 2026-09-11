using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class SetTruckCompanyHandlerTests
{
    private static Truck SomeTruck() =>
        Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);

    private static Mock<IUnitOfWork> SetUp(Truck? truck)
    {
        var trucks = new Mock<ITruckRepository>();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task SetTruckCompanyAsync_CompanyIdSupplied_CallsAssignToCompany()
    {
        var truck = SomeTruck();
        var companyId = Guid.NewGuid();
        var unitOfWork = SetUp(truck);

        var handler = new SetTruckCompanyHandler(unitOfWork.Object);
        await handler.SetTruckCompanyAsync(new SetTruckCompanyRequest(truck.Id, companyId));

        Assert.Equal(companyId, truck.TruckingCompanyId);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetTruckCompanyAsync_NullCompanyId_CallsUnassignFromCompany_NotAssignToCompanyWithNull()
    {
        var truck = SomeTruck();
        truck.AssignToCompany(Guid.NewGuid());
        var unitOfWork = SetUp(truck);

        var handler = new SetTruckCompanyHandler(unitOfWork.Object);
        await handler.SetTruckCompanyAsync(new SetTruckCompanyRequest(truck.Id, null));

        Assert.Null(truck.TruckingCompanyId);
        // UnassignFromCompany also forces IsActive false - confirms the dedicated unassign
        // path ran (not some other route that would leave IsActive whatever it was).
        Assert.False(truck.IsActive);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetTruckCompanyAsync_UnknownTruckId_Throws()
    {
        var unitOfWork = SetUp(null);

        var handler = new SetTruckCompanyHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SetTruckCompanyAsync(new SetTruckCompanyRequest(Guid.NewGuid(), Guid.NewGuid())));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
