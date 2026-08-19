using Freight.Domain.Fleet;
using Freight.Domain.Shipment;

namespace Freight.Domain.Common;

public interface IUnitOfWork
{
    ITruckingCompanyRepository TruckingCompanies { get; }

    IShipperRepository Shippers { get; }

    ITruckRepository Trucks { get; }

    IDriverRepository Drivers { get; }

    IShipmentRepository Shipments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
