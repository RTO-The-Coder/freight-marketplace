using Freight.Domain.Fleet;
using Freight.Domain.Client;
using Freight.Domain.Simulation;

namespace Freight.Domain.Common;

public interface IUnitOfWork
{
    ITruckingCompanyRepository TruckingCompanies { get; }

    IShipperRepository Shippers { get; }

    ITruckRepository Trucks { get; }

    ITripRepository Trips { get; }

    IDriverRepository Drivers { get; }

    IShipmentRepository Shipments { get; }

    ISimulationClockRepository SimulationClock { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
