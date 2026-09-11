using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Client.Enums;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Simulation;
using Freight.Domain.Simulation.Abstractions;

namespace Freight.Application.Tests.Integration.TestSupport;

/// <summary>
/// A minimal in-memory, stateful IUnitOfWork for integration scenarios: a handler's write
/// (Add, or a mutation on an already-added aggregate) must be visible to the very next
/// handler's read within the same scenario, which per-call Moq mocks cannot provide.
/// SaveChangesAsync is a no-op returning 0 - Add already "commits" into the backing
/// dictionary immediately, so there is no separate flush/rollback semantic to model here.
/// </summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public FakeTruckingCompanyRepository TruckingCompaniesRepo { get; } = new();
    public FakeShipperRepository ShippersRepo { get; } = new();
    public FakeTruckRepository TrucksRepo { get; } = new();
    public FakeTripRepository TripsRepo { get; } = new();
    public FakeDriverRepository DriversRepo { get; } = new();
    public FakeShipmentRepository ShipmentsRepo { get; } = new();
    public FakeSimulationClockRepository SimulationClockRepo { get; } = new();

    public ITruckingCompanyRepository TruckingCompanies => TruckingCompaniesRepo;
    public IShipperRepository Shippers => ShippersRepo;
    public ITruckRepository Trucks => TrucksRepo;
    public ITripRepository Trips => TripsRepo;
    public IDriverRepository Drivers => DriversRepo;
    public IShipmentRepository Shipments => ShipmentsRepo;
    public ISimulationClockRepository SimulationClock => SimulationClockRepo;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal class FakeRepository<T> where T : class
{
    protected readonly Dictionary<Guid, T> Items = [];

    public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.GetValueOrDefault(id));

    public void Add(T entity) => Items[IdOf(entity)] = entity;

    public void Remove(T entity) => Items.Remove(IdOf(entity));

    protected static Guid IdOf(T entity) => (Guid)(entity.GetType().GetProperty("Id")?.GetValue(entity)
        ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id property."));
}

internal sealed class FakeTruckingCompanyRepository : FakeRepository<TruckingCompany>, ITruckingCompanyRepository
{
    public Task<IReadOnlyList<TruckingCompany>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TruckingCompany>>([.. Items.Values]);
}

internal sealed class FakeShipperRepository : FakeRepository<Shipper>, IShipperRepository
{
    public Task<IReadOnlyList<Shipper>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Shipper>>([.. Items.Values]);
}

internal sealed class FakeTruckRepository : FakeRepository<Truck>, ITruckRepository
{
    public Task<IReadOnlyList<Truck>> GetByTruckingCompanyIdAsync(Guid truckingCompanyId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Truck>>([.. Items.Values.Where(t => t.TruckingCompanyId == truckingCompanyId)]);

    public Task<IReadOnlyList<Truck>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Truck>>([.. Items.Values]);
}

internal sealed class FakeTripRepository : FakeRepository<Trip>, ITripRepository
{
    public Task<Trip?> GetOpenTripByTruckIdAsync(Guid truckId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Values.FirstOrDefault(t => t.TruckId == truckId && t.IsOpen));

    public Task<IReadOnlyList<Trip>> GetOpenTripsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Trip>>([.. Items.Values.Where(t => t.IsOpen)]);
}

internal sealed class FakeDriverRepository : FakeRepository<Driver>, IDriverRepository
{
    public Task<IReadOnlyList<Driver>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Driver>>([.. Items.Values]);
}

internal sealed class FakeShipmentRepository : FakeRepository<Shipment>, IShipmentRepository
{
    public Task<IReadOnlyList<Shipment>> GetByShipperIdAsync(Guid shipperId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Shipment>>([.. Items.Values.Where(s => s.ShipperId == shipperId)]);

    public Task<IReadOnlyList<Shipment>> GetByStatusAsync(ShipmentStatus status, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Shipment>>([.. Items.Values.Where(s => s.Status == status)]);
}

internal sealed class FakeSimulationClockRepository : ISimulationClockRepository
{
    private SimulationClock? _clock;

    public Task<SimulationClock> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_clock ?? throw new InvalidOperationException("Simulation clock has not been created yet."));

    public Task<SimulationClock> GetOrCreateAsync(Func<DateTime> seedStartingAt, CancellationToken cancellationToken = default)
    {
        _clock ??= SimulationClock.Create(seedStartingAt());
        return Task.FromResult(_clock);
    }

    public void Add(SimulationClock clock) => _clock = clock;
}
