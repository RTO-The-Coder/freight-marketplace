using Freight.Domain.Fleet;
using Freight.Domain.Shipment;
using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.DrivingRules;
using Freight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ShipmentAggregate = Freight.Domain.Shipment.Shipment;
using ShipperAggregate = Freight.Domain.Shipment.Shipper;

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

(string Name, double Lat, double Lon) RandomPlace() => realPlaces[random.Next(realPlaces.Length)];
GeoLocation RandomLocation() { var (_, lat, lon) = RandomPlace(); return GeoLocation.Create(lat, lon); }

// ---------------------------------------------------------------------------
// 1. TruckingCompanies (8) - each with a real office location.
// ---------------------------------------------------------------------------
string[] companyNames =
[
    "Nordwest Spedition GmbH",
    "Rheinfracht Logistik AG",
    "Elbstrom Transporte KG",
    "Süddeutsche Fernfracht GmbH",
    "Alpen-Trans Spedition",
    "Ruhrgebiet Cargo Systeme",
    "Ostsee Fracht & Logistik",
    "Bayerische Güterlinie GmbH",
];

var companies = new List<TruckingCompany>();
foreach (var name in companyNames)
{
    var company = TruckingCompany.Create(Guid.NewGuid(), name, RandomLocation());
    companies.Add(company);
}

// ---------------------------------------------------------------------------
// 2. Drivers + DrivingRules - 24 distinct rule combinations
//    (2 DrivingBreakRule x 3 DailyRestRule x 2 WeeklyRestRule x
//    2 ExtendDailyDrivingWhenEligible), 4-5 Driver rows generated per combination.
//    LastName encodes the combination as a 4-letter code so the seed data is
//    self-documenting.
// ---------------------------------------------------------------------------
string BreakCode(DrivingBreakRule p) => p == DrivingBreakRule.FullBreak ? "F" : "S";
string DailyRestCode(DailyRestRule p) => p switch
{
    DailyRestRule.FullRest => "F",
    DailyRestRule.ReducedRest => "R",
    _ => "S",
};
string WeeklyRestCode(WeeklyRestRule p) => p == WeeklyRestRule.FullWeeklyRest ? "F" : "R";
string ExtendCode(bool extend) => extend ? "E" : "N";

string[] firstNames =
[
    "Anna", "Bernd", "Clara", "Dieter", "Elena", "Frank", "Greta", "Hans",
    "Ines", "Jonas", "Karla", "Lukas", "Mira", "Niklas", "Olga", "Paul",
    "Rita", "Stefan", "Tanja", "Uwe", "Vera", "Wolfgang", "Yara", "Zoe",
    "Anja", "Bastian", "Carla", "Dennis", "Eva", "Felix",
];
var firstNameIndex = 0;
string NextFirstName() => firstNames[firstNameIndex++ % firstNames.Length];

var drivers = new List<Driver>();
var driverRules = new Dictionary<Guid, DrivingRule>();

foreach (DrivingBreakRule breakPref in Enum.GetValues<DrivingBreakRule>())
{
    foreach (DailyRestRule dailyPref in Enum.GetValues<DailyRestRule>())
    {
        foreach (WeeklyRestRule weeklyPref in Enum.GetValues<WeeklyRestRule>())
        {
            foreach (var extend in new[] { true, false })
            {
                var code = $"{BreakCode(breakPref)}{DailyRestCode(dailyPref)}{WeeklyRestCode(weeklyPref)}{ExtendCode(extend)}";
                var driverCount = random.Next(4, 6); // 4 or 5 drivers per combination

                for (var i = 0; i < driverCount; i++)
                {
                    var driver = new Driver(Guid.NewGuid(), NextFirstName(), code);
                    drivers.Add(driver);
                    driverRules[driver.Id] = DrivingRule.Create(breakPref, dailyPref, weeklyPref, extend);
                }
            }
        }
    }
}

Console.WriteLine($"Generated {drivers.Count} drivers across 24 rule combinations.");

// ---------------------------------------------------------------------------
// 3. Trucks (12-15 per company) - type mix ~60% BoxTruck/15% Flatbed/
//    15% Refrigerated/10% Tanker, random capacity within realistic per-type
//    ranges, 4-5 hazmat-certified trucks flat across the whole fleet, all
//    start Idle, single driver each (drawn from the pool, never reused),
//    "bigger" trucks (>=12,000kg or >=60m3) get Team driving ~50% of the time.
// ---------------------------------------------------------------------------
TruckType RandomTruckType()
{
    var roll = random.NextDouble();
    return roll switch
    {
        < 0.60 => TruckType.BoxTruck,
        < 0.75 => TruckType.Flatbed,
        < 0.90 => TruckType.Refrigerated,
        _ => TruckType.Tanker,
    };
}

TruckCapacity RandomCapacity(TruckType type)
{
    var (weightMin, weightMax, volMin, volMax) = type switch
    {
        TruckType.BoxTruck => (3000.0, 12000.0, 15.0, 60.0),
        TruckType.Flatbed => (5000.0, 20000.0, 20.0, 50.0),
        TruckType.Refrigerated => (3000.0, 10000.0, 15.0, 40.0),
        TruckType.Tanker => (10000.0, 25000.0, 20.0, 35.0),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    var weight = weightMin + random.NextDouble() * (weightMax - weightMin);
    var volume = volMin + random.NextDouble() * (volMax - volMin);
    return new TruckCapacity(Capacity.Create(Math.Round(weight, 0), Math.Round(volume, 1)));
}

bool IsBiggerTruck(TruckCapacity capacity) =>
    capacity.Total.WeightKg >= 12000 || capacity.Total.VolumeCubicMeters >= 60;

var driverQueue = new Queue<Driver>(drivers.OrderBy(_ => random.Next()));
Driver DequeueDriver()
{
    if (driverQueue.Count == 0)
    {
        // The 24-combinations x 4-5-per-combination pool is sized for the
        // *expected* mix of Single/Team assignments, but random capacity
        // generation can occasionally push more trucks over the "bigger"
        // threshold than expected, consuming drivers faster via Team (2 each).
        // Rather than fail the whole run, top up with an overflow driver
        // outside the 24-combination scheme (still gets a real, valid
        // DrivingRule so the invariant "every Driver has a matching rule
        // entry" still holds) - keeps the seeder robust across runs
        // without hand-tuning the pool size to match one particular random
        // outcome.
        var overflowIndex = drivers.Count;
        var overflowDriver = new Driver(Guid.NewGuid(), NextFirstName(), $"OVF{overflowIndex:D3}");
        drivers.Add(overflowDriver);
        driverRules[overflowDriver.Id] = DrivingRule.Create(
            DrivingBreakRule.FullBreak,
            DailyRestRule.FullRest,
            WeeklyRestRule.FullWeeklyRest,
            extendDailyDrivingWhenEligible: false);

        return overflowDriver;
    }

    return driverQueue.Dequeue();
}

var allTrucks = new List<(Truck Truck, TruckingCompany Company)>();

// 4-5 hazmat-certified trucks flat across the whole fleet - decided up front as a
// small fixed set of (companyIndex, truckIndexWithinCompany) slots, assigned once
// truck counts per company are known below.
var hazmatSlotCount = random.Next(4, 6);
var hazmatSlots = new HashSet<(int CompanyIndex, int TruckIndex)>();

var truckCountsPerCompany = new int[companies.Count];
for (var c = 0; c < companies.Count; c++)
{
    truckCountsPerCompany[c] = random.Next(12, 16); // 12-15 inclusive
}

var totalTrucks = truckCountsPerCompany.Sum();
while (hazmatSlots.Count < hazmatSlotCount)
{
    var companyIndex = random.Next(companies.Count);
    var truckIndex = random.Next(truckCountsPerCompany[companyIndex]);
    hazmatSlots.Add((companyIndex, truckIndex));
}

for (var c = 0; c < companies.Count; c++)
{
    var company = companies[c];

    for (var t = 0; t < truckCountsPerCompany[c]; t++)
    {
        var type = RandomTruckType();
        var capacity = RandomCapacity(type);
        var hazmat = hazmatSlots.Contains((c, t));

        DriverAssignment assignment;
        if (IsBiggerTruck(capacity) && random.NextDouble() < 0.5)
        {
            var first = DequeueDriver();
            var second = DequeueDriver();
            assignment = DriverAssignment.Team(first, second);
        }
        else
        {
            assignment = DriverAssignment.Single(DequeueDriver());
        }

        var truck = new Truck(Guid.NewGuid(), company.Id, type, capacity, assignment, hazmat);
        allTrucks.Add((truck, company));
    }
}

Console.WriteLine($"Generated {totalTrucks} trucks across {companies.Count} companies " +
                   $"({hazmatSlots.Count} hazmat-certified, {driverQueue.Count} drivers left unused).");

// ---------------------------------------------------------------------------
// 4. Shippers - a small shared pool, reused across multiple shipments.
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
// 5. Shipments - one per truck, cargo kind matched to the truck's type per
//    Section 8.1's compatibility table, dummy pickup/delivery locations and
//    time windows, weight/volume sized to fit within the truck's capacity.
//    Stop.ExpectedArrivalTime is a placeholder - there is no real route-time
//    engine yet (Slice 4) to compute a genuine arrival time.
// ---------------------------------------------------------------------------
CargoKind CargoKindFor(TruckType type, bool hazmat)
{
    if (hazmat)
    {
        return CargoKind.HazardousMaterials;
    }

    return type switch
    {
        TruckType.Tanker => CargoKind.LiquidBulk,
        TruckType.Refrigerated => CargoKind.PerishableTemperatureControlled,
        TruckType.Flatbed => random.NextDouble() < 0.5 ? CargoKind.GeneralDryGoods : CargoKind.OversizedIrregular,
        _ => CargoKind.GeneralDryGoods,
    };
}

var baseDate = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc); // a Monday, arbitrary near-future anchor
var shipments = new List<ShipmentAggregate>();

foreach (var (truck, _) in allTrucks)
{
    var shipper = shippers[random.Next(shippers.Count)];
    var cargoKind = CargoKindFor(truck.TruckType, truck.HazmatCertified);

    // Cargo size: a random fraction of the truck's total capacity, always leaving
    // room within Remaining (checked by Truck.AssignShipment itself).
    var fraction = 0.3 + random.NextDouble() * 0.5; // 30-80% of capacity
    var cargoSize = Capacity.Create(
        Math.Round(truck.Capacity.Total.WeightKg * fraction, 0),
        Math.Round(truck.Capacity.Total.VolumeCubicMeters * fraction, 1));

    var dayOffset = random.Next(0, 14);
    var pickupStart = baseDate.AddDays(dayOffset).AddHours(6 + random.Next(0, 6));
    var pickupEnd = pickupStart.AddHours(2 + random.Next(0, 3));
    var deliveryDeadline = pickupEnd.AddHours(4 + random.Next(0, 20));

    var shipment = new ShipmentAggregate(
        Guid.NewGuid(),
        shipper.Id,
        RandomLocation(),
        RandomLocation(),
        cargoKind,
        cargoSize,
        pickupStart,
        pickupEnd,
        deliveryDeadline);

    shipments.Add(shipment);

    // Dummy arrival-time placeholders: pickup at the window's start, delivery at
    // the deadline - not a real route-time calculation (Slice 4 doesn't exist
    // yet), just a plausible-looking value for this seed pass.
    truck.AssignShipment(
        shipment.Id,
        cargoSize,
        pickupInsertIndex: 0,
        deliveryInsertIndex: 0,
        pickupExpectedArrivalTime: pickupStart,
        deliveryExpectedArrivalTime: deliveryDeadline);
}

Console.WriteLine($"Generated {shipments.Count} shipments (one per truck), each with a Pickup+Delivery Stop.");

// ---------------------------------------------------------------------------
// Persist everything. DriverComplianceStates, driver DrivingRules,
// RouteProgresses, and RouteLegs are intentionally left empty for
// this pass - all trucks are Idle with no active tick-engine state, and
// DrivingRule persistence is not yet wired up.
//
// TruckingCompany and Shipper are explicitly added here (Slice 2) now that
// TruckingCompany no longer owns Truck as a navigation EF could discover them
// through transitively. Truck/Driver/Shipment tracking is a pre-existing gap
// (nothing in this seeder ever added them explicitly either) left for a future
// slice to address alongside Truck's proper redesign.
// ---------------------------------------------------------------------------

db.AddRange(companies);
db.AddRange(shippers);

await db.SaveChangesAsync();

Console.WriteLine("Seed data committed.");
Console.WriteLine($"  TruckingCompanies: {companies.Count}");
Console.WriteLine($"  Trucks: {totalTrucks}");
Console.WriteLine($"  Drivers: {drivers.Count}");
Console.WriteLine($"  Shippers: {shippers.Count}");
Console.WriteLine($"  Shipments: {shipments.Count}");
