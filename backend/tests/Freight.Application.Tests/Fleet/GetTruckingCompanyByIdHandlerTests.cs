using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class GetTruckingCompanyByIdHandlerTests
{
    [Fact]
    public async Task GetTruckingCompanyByIdAsync_KnownId_ReturnsSameShapeAsListEndpoint()
    {
        var location = GeoLocation.Create(52.52, 13.405);
        var company = TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", location);
        var companies = new Mock<ITruckingCompanyRepository>();
        companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.TruckingCompanies).Returns(companies.Object);

        var handler = new GetTruckingCompanyByIdHandler(unitOfWork.Object);
        var dto = await handler.GetTruckingCompanyByIdAsync(new GetTruckingCompanyByIdRequest(company.Id));

        Assert.Equal(company.Id, dto.CompanyId);
        Assert.Equal(company.Name, dto.Name);
        Assert.Equal(location.Latitude, dto.OfficeLatitude);
        Assert.Equal(location.Longitude, dto.OfficeLongitude);
    }

    [Fact]
    public async Task GetTruckingCompanyByIdAsync_UnknownId_Throws()
    {
        var companies = new Mock<ITruckingCompanyRepository>();
        companies.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((TruckingCompany?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.TruckingCompanies).Returns(companies.Object);

        var handler = new GetTruckingCompanyByIdHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.GetTruckingCompanyByIdAsync(new GetTruckingCompanyByIdRequest(Guid.NewGuid())));
    }
}
