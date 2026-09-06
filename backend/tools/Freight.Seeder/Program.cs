using Freight.Domain.Client;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Simulation;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ShipmentAggregate = Freight.Domain.Client.Shipment;
using ShipperAggregate = Freight.Domain.Client.Shipper;

// Allow overriding the target DB via CLI arg so the seeder can be pointed at
// non-local environments without editing source.
var connectionString = args.Length > 0
    ? args[0]
    : "Host=localhost;Port=5432;Database=freight_marketplace;Username=freight;Password=freight_dev_password";

var optionsBuilder = new DbContextOptionsBuilder<FreightDbContext>();
optionsBuilder.UseNpgsql(connectionString);

await using var db = new FreightDbContext(optionsBuilder.Options);

// Fixed seed for reproducible runs - re-running the seeder from a clean database
// always produces the same dataset.
var random = new Random(20260817);

// ---------------------------------------------------------------------------
// Real, named, suburb/district-level locations only - never street addresses,
// per the project's geography/privacy convention (docs/design/client-architecture-
// and-operations.md, "Location granularity" - sourced the same way city centroids
// are, from publicly-documented place coordinates, not synthetic street addresses).
// ---------------------------------------------------------------------------
var realPlaces = new (string Name, double Lat, double Lon)[]
{
    ("Berlin-Mitte", 52.5200, 13.4050),
    ("Hamburg-Altona", 53.5511, 9.9349),
    ("Munich-Schwabing", 48.1642, 11.5822),
    ("Cologne-Ehrenfeld", 50.9540, 6.9200),
    ("Frankfurt-Sachsenhausen", 50.1010, 8.6821),
    ("Stuttgart-West", 48.7784, 9.1642),
    ("Dusseldorf-Oberkassel", 51.2270, 6.7580),
    ("Leipzig-Plagwitz", 51.3300, 12.3300),
    ("Dresden-Neustadt", 51.0637, 13.7461),
    ("Hannover-Linden", 52.3700, 9.6900),
    ("Nuremberg-Gostenhof", 49.4480, 11.0500),
    ("Bremen-Viertel", 53.0730, 8.8110),
    ("Essen-Rüttenscheid", 51.4380, 7.0140),
    ("Dortmund-Kreuzviertel", 51.5090, 7.4460),
    ("Bonn-Poppelsdorf", 50.7280, 7.0800),
    ("Mannheim-Neckarstadt", 49.5000, 8.4700),
    ("Karlsruhe-Südstadt", 49.0020, 8.4000),
    ("Freiburg-Wiehre", 47.9870, 7.8500),
    ("Münster-Kreuzviertel", 51.9560, 7.6100),
    ("Augsburg-Antonsviertel", 48.3660, 10.8940),
};

// Picks one of the fixed real places at random - used everywhere a location is needed.
(string Name, double Lat, double Lon) RandomPlace() => realPlaces[random.Next(realPlaces.Length)];
// Convenience wrapper: most call sites only need the coordinates, not the place name.
GeoLocation RandomLocation() { var (_, lat, lon) = RandomPlace(); return GeoLocation.Create(lat, lon); }

// Weighted draw matching the ~40/35/25 Small/Medium/Large truck-size mix, used
// only to pick a plausible capacity tier to size Shipment loads against - there
// are no actual Trucks in this seed pass.
TruckSize RandomTruckSize()
{
    var roll = random.NextDouble();
    return roll switch
    {
        < 0.40 => TruckSize.Small,
        < 0.75 => TruckSize.Medium,
        _ => TruckSize.Large,
    };
}

// ---------------------------------------------------------------------------
// Reseed - deletes every row (TRUNCATE ... CASCADE handles FK dependency order
// automatically, so table order doesn't matter) and repopulates exactly four
// things: TruckingCompanies, Shippers, SimulationClock, and Shipments. No
// Trucks/Drivers/Trips this pass.
// ---------------------------------------------------------------------------
async Task Reseed()
{
    // Fixed literal table names, not user input - table/column identifiers can't be
    // parameterized anyway (only values can), so the EF1002 SQL-injection analyzer
    // warning doesn't apply here.
#pragma warning disable EF1002
    string[] tables =
    [
        "Shipments", "TripStops", "Trips", "TruckRouteProgresses",
        "DriverComplianceStates", "Trucks", "Drivers", "TruckingCompanies",
        "Shippers", "SimulationClock",
    ];
    foreach (var table in tables)
    {
        await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{table}\" CASCADE;");
    }
#pragma warning restore EF1002
    Console.WriteLine("Cleared all data.");

    // 1. TruckingCompanies (5) - each with a real office location.
    string[] companyNames =
    [
        "Nordwest Spedition GmbH",
        "Rheinfracht Logistik AG",
        "Elbstrom Transporte KG",
        "Süddeutsche Fernfracht GmbH",
        "Alpen-Trans Spedition",
    ];

    var companies = companyNames
        .Select(name => TruckingCompany.Create(Guid.NewGuid(), name, RandomLocation()))
        .ToList();
    db.AddRange(companies);
    Console.WriteLine($"Generated {companies.Count} trucking companies.");

    // 2. Shippers - a small shared reference pool.
    string[] shipperNames =
    [
        "Markus Weber", "Sabine Hoffmann", "Thomas Becker", "Julia Schulz",
        "Andreas Wolf", "Petra Neumann", "Michael Krause", "Claudia Richter",
        "Stefan Lange", "Nicole Vogel", "Christian Fischer", "Birgit Schwarz",
        "Matthias Zimmermann", "Sandra Braun", "Jürgen Krüger", "Monika Hartmann",
        "Alexander Werner", "Susanne Schmitt", "Peter Lehmann", "Karin Huber",
    ];

    var shippers = shipperNames
        .Select(name => ShipperAggregate.Create(
            Guid.NewGuid(),
            name,
            $"{name.ToLowerInvariant().Replace(" ", ".")}@example.com"))
        .ToList();
    db.AddRange(shippers);
    Console.WriteLine($"Generated {shippers.Count} shippers.");

    // 3. SimulationClock - the single global "now" for the whole system, set to a
    // fixed demo anchor rather than real time.
    var demoAnchor = new DateTime(2026, 8, 1, 5, 0, 0, DateTimeKind.Utc);
    var clock = SimulationClock.Create(demoAnchor);
    db.Add(clock);
    Console.WriteLine($"SimulationClock set to {demoAnchor:yyyy-MM-dd HH:mm} UTC.");

    // 4. Shipments - 20 total, 5 per TruckType (BoxVan, Flatbed, Refrigerated,
    // Tanker). Per type: 1 "anchor" shipment with a pickup window starting
    // randomly between 07:00-11:00 on the clock's start date, and 4 more
    // shipments each scattered 10-12 hours after that anchor's pickup start -
    // all relative to demoAnchor (the same value just written to SimulationClock),
    // not real time. Booked via Shipment.Book(...) - all start Pending, no
    // TruckingCompanyId, exactly as a real Shipper submission would.
    var shipments = new List<ShipmentAggregate>();
    foreach (TruckType truckType in Enum.GetValues<TruckType>())
    {
        // Anchor shipment: pickup starts randomly between 07:00 and 11:00 on the
        // clock's start date.
        var anchorPickupStart = demoAnchor.Date.AddHours(7).AddMinutes(random.Next(0, 4 * 60 + 1));

        for (var i = 0; i < 5; i++)
        {
            // i == 0 is the anchor itself; i == 1..4 are scattered 10-12 hours
            // after the anchor's pickup start, each independently randomized
            // within that window.
            var pickupStart = i == 0
                ? anchorPickupStart
                : anchorPickupStart.AddHours(10).AddMinutes(random.Next(0, 2 * 60 + 1));

            var pickupEnd = pickupStart.AddHours(2 + random.Next(0, 3));
            var deliveryStart = pickupEnd.AddHours(4 + random.Next(0, 20));
            var deliveryEnd = deliveryStart.AddHours(2 + random.Next(0, 6));
            var bookedAt = demoAnchor;

            var shipper = shippers[random.Next(shippers.Count)];

            // Kept deliberately small (10-25% of a tier's capacity, vs. a larger
            // fraction) - these shipments are meant to become stops added onto a
            // truck's route alongside others, so each one should leave plenty of
            // remaining capacity for further stops to be added later.
            var sizeTier = RandomTruckSize();
            var fraction = 0.10 + random.NextDouble() * 0.15; // 10-25% of the tier's capacity
            var tierCapacity = Capacity.ForTruckSize(sizeTier);
            var load = Capacity.Create(
                Math.Round(tierCapacity.WeightKg * fraction, 0),
                Math.Round(tierCapacity.VolumeCubicMeters * fraction, 1));

            // Pickup and delivery must be different real places.
            var pickupLocation = RandomLocation();
            GeoLocation deliveryLocation;
            do
            {
                deliveryLocation = RandomLocation();
            } while (deliveryLocation.Latitude == pickupLocation.Latitude && deliveryLocation.Longitude == pickupLocation.Longitude);

            var shipment = ShipmentAggregate.Book(
                shipper.Id,
                pickupLocation,
                deliveryLocation,
                load,
                truckType,
                TimeWindow.Create(pickupStart, pickupEnd),
                TimeWindow.Create(deliveryStart, deliveryEnd),
                bookedAt);

            shipments.Add(shipment);
        }
    }

    db.AddRange(shipments);
    Console.WriteLine($"Generated {shipments.Count} shipments (5 per TruckType).");

    await db.SaveChangesAsync();

    Console.WriteLine("Seed data committed.");
    Console.WriteLine($"  TruckingCompanies: {companies.Count}");
    Console.WriteLine($"  Shippers: {shippers.Count}");
    Console.WriteLine($"  Shipments: {shipments.Count}");
}

await Reseed();
