using Freight.Domain.Fleet;
using Freight.Domain.Shipment;
using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Freight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ShipmentAggregate = Freight.Domain.Shipment.Shipment;
using ShipperAggregate = Freight.Domain.Shipment.Shipper;

// Allow overriding the target DB via CLI arg so the seeder can be pointed at
// non-local environments without editing source.
var connectionString = args.Length > 0
    ? args[0]
    : "Host=localhost;Port=5432;Database=freight_marketplace;Username=freight;Password=freight_dev_password";

var optionsBuilder = new DbContextOptionsBuilder<FreightDbContext>();
optionsBuilder.UseNpgsql(connectionString);

await using var db = new FreightDbContext(optionsBuilder.Options);

// Wipes all seeded data while leaving the schema (tables/migrations) in place -
// TRUNCATE ... CASCADE handles FK dependency order automatically, so table order
// here doesn't matter. Safe to call against a DB that already has the current
// migrations applied; does nothing to the schema itself.
async Task ClearDatabase()
{
    // Fixed literal table names, not user input - table/column identifiers can't be
    // parameterized anyway (only values can), so the EF1002/EF1003 SQL-injection
    // analyzer warning doesn't apply here.
#pragma warning disable EF1002
    string[] tables = ["Shipments", "TruckRouteStops", "Trucks", "Drivers", "TruckingCompanies", "Shippers"];
    foreach (var table in tables)
    {
        await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{table}\" CASCADE;");
    }
#pragma warning restore EF1002
}

await ClearDatabase();
Console.WriteLine("Cleared existing data.");

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

// ---------------------------------------------------------------------------
// 1. TruckingCompanies (5) - each with a real office location. Kept small so
//    the seeded fleet counts stay easy to eyeball in dev/demo use.
// ---------------------------------------------------------------------------
string[] companyNames =
[
    "Nordwest Spedition GmbH",
    "Rheinfracht Logistik AG",
    "Elbstrom Transporte KG",
    "Süddeutsche Fernfracht GmbH",
    "Alpen-Trans Spedition",
];

var companies = new List<TruckingCompany>();
foreach (var name in companyNames)
{
    // Each company gets a random real place as its office location - see the
    // "real, named" convention above.
    var company = TruckingCompany.Create(Guid.NewGuid(), name, RandomLocation());
    companies.Add(company);
}

// ---------------------------------------------------------------------------
// 2. Drivers + DrivingRules - a fixed pool of 17: 12 will be assigned to the
//    10 trucks below (2 Team trucks x 2 + 8 Single trucks x 1), the remaining
//    5 stay unassigned to any truck. Each driver's DrivingRules is picked
//    independently at random across all 4 rule dimensions, rather than
//    generated from the systematic combination scheme.
// ---------------------------------------------------------------------------
const int DriverCount = 17;

string[] firstNames =
[
    "Anna", "Bernd", "Clara", "Dieter", "Elena", "Frank", "Greta", "Hans",
    "Ines", "Jonas", "Karla", "Lukas", "Mira", "Niklas", "Olga", "Paul",
    "Rita", "Stefan", "Tanja", "Uwe", "Vera", "Wolfgang", "Yara", "Zoe",
    "Anja", "Bastian", "Carla", "Dennis", "Eva", "Felix",
];
// Cycles through the fixed name list rather than picking randomly, so first names
// stay distinct as long as possible before repeating.
var firstNameIndex = 0;
string NextFirstName() => firstNames[firstNameIndex++ % firstNames.Length];

// Picks a uniformly random value from an enum - used to draw each of the 4
// DrivingRules dimensions independently.
T RandomEnumValue<T>() where T : struct, Enum
{
    var values = Enum.GetValues<T>();
    return values[random.Next(values.Length)];
}

var drivers = new List<Driver>();
for (var i = 0; i < DriverCount; i++)
{
    var rules = DrivingRules.Create(
        RandomEnumValue<DrivingBreakRule>(),
        RandomEnumValue<DailyRestRule>(),
        RandomEnumValue<WeeklyRestRule>(),
        extendDailyDrivingWhenEligible: random.Next(2) == 0);

    // LastName is just a sequential label now that there's no fixed rule-combination
    // code to encode (rules are random per-driver, not drawn from a fixed scheme).
    var driver = Driver.Create(Guid.NewGuid(), NextFirstName(), $"D{i + 1:D2}", rules);
    drivers.Add(driver);
}

Console.WriteLine($"Generated {drivers.Count} drivers with randomly assigned driving rules.");

// ---------------------------------------------------------------------------
// 3. Trucks (10 total) - fixed scenario rather than randomized scale:
//    7 assigned across all 5 companies (one company gets 3, the other 4 get 1
//    each), 3 left unassigned (no company, mirrors a truck not yet onboarded).
//    Every truck gets a driver regardless of assignment status. Exactly 2
//    trucks are hazmat-certified and exactly 2 run Team driving (forced to
//    TruckSize.Large, since DriverAssignment.Team requires it - see
//    Fleet/DriverAssignment.cs). Type/size for the rest is still the weighted
//    random draw (capacity is derived from size, never entered independently -
//    FR1.2).
// ---------------------------------------------------------------------------
// Weighted draw matching the ~60/15/15/10 BoxVan/Flatbed/Refrigerated/Tanker mix.
TruckType RandomTruckType()
{
    var roll = random.NextDouble();
    return roll switch
    {
        < 0.60 => TruckType.BoxVan,
        < 0.75 => TruckType.Flatbed,
        < 0.90 => TruckType.Refrigerated,
        _ => TruckType.Tanker,
    };
}

// Weighted draw matching the ~40/35/25 Small/Medium/Large mix - overridden to
// Large for the 2 slots chosen for Team driving (see below).
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

const int TruckCount = 10;

// How many of the 7 assigned trucks each company gets: index 0 gets 3, the
// remaining 4 companies get 1 each - matches the fixed scenario exactly.
var truckCountsPerCompany = new int[companies.Count];
truckCountsPerCompany[0] = 3;
for (var c = 1; c < companies.Count; c++)
{
    truckCountsPerCompany[c] = 1;
}
var assignedTruckCount = truckCountsPerCompany.Sum(); // 7
var unassignedTruckCount = TruckCount - assignedTruckCount; // 3

// Pick which 2 of the 10 truck slots (0..9, assigned trucks first then
// unassigned) are hazmat-certified and which 2 run Team, independently and
// without regard to company assignment - both picks may overlap each other.
HashSet<int> RandomDistinctSlots(int count, int total)
{
    var slots = new HashSet<int>();
    while (slots.Count < count)
    {
        slots.Add(random.Next(total));
    }
    return slots;
}
var hazmatSlots = RandomDistinctSlots(2, TruckCount);
var teamSlots = RandomDistinctSlots(2, TruckCount);

// Shuffled so drivers aren't handed out in a predictable order.
var driverQueue = new Queue<Driver>(drivers.OrderBy(_ => random.Next()));

var allTrucks = new List<(Truck Truck, TruckingCompany? Company)>();
var slotIndex = 0;

void BuildTruck(TruckingCompany? company, string name)
{
    var hazmat = hazmatSlots.Contains(slotIndex);
    var isTeam = teamSlots.Contains(slotIndex);
    slotIndex++;

    var type = RandomTruckType();
    // Team driving requires a Large truck (DriverAssignment.Team), so force it
    // for the 2 chosen Team slots regardless of the random size draw.
    var size = isTeam ? TruckSize.Large : RandomTruckSize();

    var truck = Truck.Create(Guid.NewGuid(), name, type, size);
    if (company is not null)
    {
        truck.AssignToCompany(company.Id);
        truck.Activate();
    }

    if (hazmat)
    {
        truck.CertifyForHazmat();
    }

    if (isTeam)
    {
        truck.AssignDrivers(driverQueue.Dequeue(), driverQueue.Dequeue());
    }
    else
    {
        truck.AssignDrivers(driverQueue.Dequeue());
    }

    allTrucks.Add((truck, company));
}

for (var c = 0; c < companies.Count; c++)
{
    var company = companies[c];
    for (var t = 0; t < truckCountsPerCompany[c]; t++)
    {
        BuildTruck(company, $"{company.Name} #{t + 1:D2}");
    }
}

for (var u = 0; u < unassignedTruckCount; u++)
{
    BuildTruck(null, $"Unassigned Truck #{u + 1:D2}");
}

Console.WriteLine($"Generated {allTrucks.Count} trucks ({assignedTruckCount} assigned across " +
                   $"{companies.Count} companies, {unassignedTruckCount} unassigned; " +
                   $"{hazmatSlots.Count} hazmat-certified, {teamSlots.Count} Team-driven; " +
                   $"{driverQueue.Count} drivers left unassigned).");

// ---------------------------------------------------------------------------
// 4. Shippers - a small standalone reference pool (not consumed by anything
//    else this pass, since there are no Shipments here).
// ---------------------------------------------------------------------------
string[] shipperNames =
[
    "Markus Weber", "Sabine Hoffmann", "Thomas Becker", "Julia Schulz",
    "Andreas Wolf", "Petra Neumann", "Michael Krause", "Claudia Richter",
    "Stefan Lange", "Nicole Vogel", "Christian Fischer", "Birgit Schwarz",
    "Matthias Zimmermann", "Sandra Braun", "Jürgen Krüger", "Monika Hartmann",
    "Alexander Werner", "Susanne Schmitt", "Peter Lehmann", "Karin Huber",
];

var shippers = shipperNames
    .Select((name, i) => ShipperAggregate.Create(
        Guid.NewGuid(),
        name,
        $"{name.ToLowerInvariant().Replace(" ", ".")}@example.com"))
    .ToList();

Console.WriteLine($"Generated {shippers.Count} shippers.");

// ---------------------------------------------------------------------------
// 5. Shipments - exactly 5 Shippers used (the first 5 from the pool above),
//    each with exactly 3 Shipments (15 total). Booked via Shipment.Book(...)
//    - all start Pending, no TruckingCompanyId, exactly as a real Shipper
//    submission would; Book does not require a truck to exist yet, so these
//    are independent of the fixed fleet scenario above. Load is sized as a
//    random fraction of a randomly picked truck-size tier (Capacity.ForTruckSize)
//    since there's no specific truck to size against at booking time.
// ---------------------------------------------------------------------------
const int ShippersWithShipments = 5;
const int ShipmentsPerShipper = 3;

var baseDate = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc); // a Monday, arbitrary near-future anchor
var shipments = new List<ShipmentAggregate>();

foreach (var shipper in shippers.Take(ShippersWithShipments))
{
    for (var s = 0; s < ShipmentsPerShipper; s++)
    {
        var sizeTier = RandomTruckSize();
        var fraction = 0.3 + random.NextDouble() * 0.5; // 30-80% of the tier's capacity
        var tierCapacity = Capacity.ForTruckSize(sizeTier);
        var load = Capacity.Create(
            Math.Round(tierCapacity.WeightKg * fraction, 0),
            Math.Round(tierCapacity.VolumeCubicMeters * fraction, 1));

        // Spread pickups across a two-week window so shipments don't all land on
        // one day; pickup/delivery windows and booking lead time are all
        // randomized within plausible ranges to avoid every shipment looking
        // identical.
        var dayOffset = random.Next(0, 14);
        var pickupStart = baseDate.AddDays(dayOffset).AddHours(6 + random.Next(0, 6));
        var pickupEnd = pickupStart.AddHours(2 + random.Next(0, 3));
        var deliveryStart = pickupEnd.AddHours(4 + random.Next(0, 20));
        var deliveryEnd = deliveryStart.AddHours(2 + random.Next(0, 6));
        var bookedAt = pickupStart.AddDays(-1 - random.Next(0, 3));

        var shipment = ShipmentAggregate.Book(
            shipper.Id,
            RandomLocation(),
            RandomLocation(),
            load,
            RandomTruckType(),
            TimeWindow.Create(pickupStart, pickupEnd),
            TimeWindow.Create(deliveryStart, deliveryEnd),
            bookedAt);

        shipments.Add(shipment);
    }
}

Console.WriteLine($"Generated {shipments.Count} shipments across {ShippersWithShipments} shippers " +
                   $"({ShipmentsPerShipper} each).");

// ---------------------------------------------------------------------------
// Persist everything.
// ---------------------------------------------------------------------------

db.AddRange(companies);
db.AddRange(shippers);
db.AddRange(drivers);
db.AddRange(allTrucks.Select(x => x.Truck));
db.AddRange(shipments);

await db.SaveChangesAsync();

Console.WriteLine("Seed data committed.");
Console.WriteLine($"  TruckingCompanies: {companies.Count}");
Console.WriteLine($"  Trucks: {allTrucks.Count}");
Console.WriteLine($"  Drivers: {drivers.Count}");
Console.WriteLine($"  Shippers: {shippers.Count}");
Console.WriteLine($"  Shipments: {shipments.Count}");
