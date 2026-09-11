using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class GetTruckingCompaniesHandlerTests
{
    [Fact]
    public async Task GetTruckingCompaniesAsync_MapsIdNameAndOfficeCoordinates()
    {
        var location = GeoLocation.Create(52.52, 13.405);
        var company = TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", location);
        var companies = new Mock<ITruckingCompanyRepository>();
        companies.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([company]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.TruckingCompanies).Returns(companies.Object);

        var handler = new GetTruckingCompaniesHandler(unitOfWork.Object);
        var response = await handler.GetTruckingCompaniesAsync();

        var dto = Assert.Single(response.Companies);
        Assert.Equal(company.Id, dto.CompanyId);
        Assert.Equal(company.Name, dto.Name);
        Assert.Equal(location.Latitude, dto.OfficeLatitude);
        Assert.Equal(location.Longitude, dto.OfficeLongitude);
    }

    [Fact]
    public async Task GetTruckingCompaniesAsync_NoCompanies_ReturnsEmptyNotNullList()
    {
        var companies = new Mock<ITruckingCompanyRepository>();
        companies.Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.TruckingCompanies).Returns(companies.Object);

        var handler = new GetTruckingCompaniesHandler(unitOfWork.Object);
        var response = await handler.GetTruckingCompaniesAsync();

        Assert.NotNull(response.Companies);
        Assert.Empty(response.Companies);
    }
}
