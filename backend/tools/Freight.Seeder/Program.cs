using System.Text.Json;
using Freight.Domain.Client;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Simulation;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Persistence;
using Freight.Seeder.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ShipmentAggregate = Freight.Domain.Client.Shipment;
using ShipperAggregate = Freight.Domain.Client.Shipper;

// --build-fixture: one-time/rarely-rerun mode that calls the real OSRM routing service for
// every pair in LocationPairs.All, names every place by its area on Nominatim, and writes
// Fixtures/route-pairs.json. The ordinary seed run below never takes this path and never
// calls OSRM or Nominatim itself - see Fixtures/FixtureBuilder.cs.
if (args.Contains("--build-fixture"))
{
    // Write directly into the source tree (found by walking up from the build output to the
    // .csproj), not the build output's copy, so the result is immediately the file `git add`
    // picks up - no manual copy-back step after running this.
    var fixtureOutputPath = Path.Combine(FindProjectSourceDirectory(), "Fixtures", "route-pairs.json");
    await FixtureBuilder.RunAsync(fixtureOutputPath);
    return;
}

static string FindProjectSourceDirectory()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !dir.GetFiles("Freight.Seeder.csproj").Any())
    {
        dir = dir.Parent;
    }

    return dir?.FullName
        ?? throw new InvalidOperationException("Could not locate Freight.Seeder.csproj above the build output directory.");
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Allow overriding the target DB via CLI arg so the seeder can be pointed at
// non-local environments without editing source; falls back to config/appsettings.
var connectionString = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
    ? args[0]
    : configuration.GetConnectionString("FreightDb")
        ?? "Host=localhost;Port=5432;Database=freight_marketplace;Username=freight;Password=freight_dev_password";

// The single anchor every seeded date is computed relative to. Changing this value (via
// appsettings.json or the Seeder__AnchorUtc env var) and re-running the seeder shifts the
// entire dataset consistently - e.g. an anchor of 2026-02-18T05:00Z vs. 2026-08-20T07:00Z
// produces the same relative shape, just moved.
//
// Parsed via DateTimeOffset, not IConfiguration's plain DateTime binder: the latter parses
// an ISO "...Z" string and converts it to the machine's LOCAL time zone, silently losing
// the UTC instant it named - a later DateTime.SpecifyKind(..., Utc) would only relabel that
// already-shifted local value, not correct it.
var anchorSetting = configuration.GetValue<string?>("Seeder:AnchorUtc");
var anchorUtc = anchorSetting is not null
    ? DateTimeOffset.Parse(anchorSetting, null, System.Globalization.DateTimeStyles.AssumeUniversal).UtcDateTime
    : new DateTime(2026, 8, 1, 5, 0, 0, DateTimeKind.Utc);

var optionsBuilder = new DbContextOptionsBuilder<FreightDbContext>();
optionsBuilder.UseNpgsql(connectionString);

await using var db = new FreightDbContext(optionsBuilder.Options);

// Fixed seed for reproducible runs - re-running the seeder from a clean database
// always produces the same dataset (relative to whatever anchorUtc is configured).
var random = new Random(20260817);

// Saved-address names draw from their own sequence, so adding address books left every draw
// above (shippers, windows, loads) - and so the seeded shipments - unchanged.
var addressRandom = new Random(20260818);

// ---------------------------------------------------------------------------
// Route-pair fixture - real OSRM-computed distance/time per pickup->delivery pair,
// precomputed once via `dotnet run --build-fixture` (see Fixtures/FixtureBuilder.cs) and
// committed to the repo. Loaded here instead of picking two independent random locations,
// so every shipment's geography is real and every window derived below is grounded in an
// actual road distance/drive-time rather than a guessed hour range.
// ---------------------------------------------------------------------------
var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "route-pairs.json");
if (!File.Exists(fixturePath))
{
    Console.WriteLine($"Route-pair fixture not found at {fixturePath}.");
    Console.WriteLine("Run `dotnet run --build-fixture` once to generate it (calls OSRM; requires network access).");
    return;
}

var fixtureJson = await File.ReadAllTextAsync(fixturePath);
var routePairs = JsonSerializer.Deserialize<List<RoutePairFixture>>(fixtureJson)
    ?? throw new InvalidOperationException($"Fixture file at {fixturePath} deserialized to null/empty.");

if (routePairs.Count == 0)
{
    throw new InvalidOperationException($"Fixture file at {fixturePath} contains no route pairs.");
}

RoutePairFixture RoutePair(string pairId) =>
    routePairs.SingleOrDefault(p => p.PairId == pairId)
    ?? throw new InvalidOperationException($"Route pair '{pairId}' is not in the fixture - run `dotnet run --build-fixture`.");

// A fixture place's coordinates by its name (company offices are fixture places too).
GeoLocation PlaceLocation(string placeName)
{
    var pair = routePairs.FirstOrDefault(p => p.PickupName == placeName || p.DeliveryName == placeName)
        ?? throw new InvalidOperationException($"Place '{placeName}' is not in the route-pair fixture.");
    return pair.PickupName == placeName
        ? GeoLocation.Create(pair.PickupLat, pair.PickupLon)
        : GeoLocation.Create(pair.DeliveryLat, pair.DeliveryLon);
}

// A fixture place's generic area, e.g. "1100 Favoriten, Vienna" for "Vienna-Favoriten" - what
// seeded addresses, shipments and offices are named by (ADR 0013). Postcode and city were looked
// up once by --build-fixture; the suburb is the place name's own (after the city), just as a
// shipper's typed suburb names a real saved address.
string PlaceArea(string placeName)
{
    var pair = routePairs.FirstOrDefault(p => p.PickupName == placeName || p.DeliveryName == placeName)
        ?? throw new InvalidOperationException($"Place '{placeName}' is not in the route-pair fixture.");
    var (postcode, city) = pair.PickupName == placeName
        ? (pair.PickupPostcode, pair.PickupCity)
        : (pair.DeliveryPostcode, pair.DeliveryCity);
    if (string.IsNullOrWhiteSpace(city))
    {
        throw new InvalidOperationException($"Place '{placeName}' has no city in the fixture - run `dotnet run --build-fixture`.");
    }

    var suburb = placeName.Contains('-') ? placeName[(placeName.IndexOf('-') + 1)..] : placeName;
    return ShipperAddress.ComposeAreaName(postcode, suburb, city);
}

// ---------------------------------------------------------------------------
// Address books - every seeded shipment is booked the way the shipper app books one: from two
// of its shipper's saved addresses, copying their place name ("Warehouse — 1100 Favoriten,
// Vienna") and coordinates. A shipper's address for a fixture place is created the first time
// one of its shipments uses that place, under a business-style name unique within its book.
// ---------------------------------------------------------------------------
string[] addressLabels =
[
    "Warehouse", "Depot", "Distribution centre", "Plant", "Branch office", "Logistics hub",
    "Factory", "Cross-dock", "Supplier site", "Customer site", "Store", "Showroom",
];
var savedAddresses = new Dictionary<(Guid ShipperId, string PlaceName), ShipperAddress>();

ShipperAddress SavedAddress(ShipperAggregate shipper, string placeName, DateTime createdAt)
{
    if (savedAddresses.TryGetValue((shipper.Id, placeName), out var saved))
    {
        return saved;
    }

    var usedLabels = shipper.Addresses.Select(address => address.Label).ToHashSet();
    var freeLabels = addressLabels.Where(label => !usedLabels.Contains(label)).ToList();
    var label = freeLabels.Count > 0
        ? freeLabels[addressRandom.Next(freeLabels.Count)]
        : $"Site {shipper.Addresses.Count + 1}";

    saved = shipper.AddAddress(label, PlaceArea(placeName), PlaceLocation(placeName), createdAt);
    savedAddresses[(shipper.Id, placeName)] = saved;
    return saved;
}

ShipmentAggregate BookFromAddressBook(
    ShipperAggregate shipper,
    RoutePairFixture pair,
    Capacity load,
    TruckType truckType,
    TimeWindow pickupWindow,
    TimeWindow deliveryWindow,
    DateTime bookedAt,
    Guid? directCompanyId = null)
{
    var pickup = SavedAddress(shipper, pair.PickupName, bookedAt);
    var delivery = SavedAddress(shipper, pair.DeliveryName, bookedAt);
    return ShipmentAggregate.Book(
        shipper.Id,
        pickup.Location,
        pickup.PlaceName,
        delivery.Location,
        delivery.PlaceName,
        load,
        truckType,
        pickupWindow,
        deliveryWindow,
        bookedAt,
        directCompanyId);
}

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

// Derives a realistic (pickupWindow, deliveryWindow) pair from a route pair's real
// DistanceKm/TimeTicks, instead of the old fixed-hour-offset guesses. The delivery window
// is sized so it's actually reachable given EU mandatory rest breaks - not just bare drive
// time - by padding in a rest/scheduling allowance proportional to the tier:
//   - Short/Medium: no mandatory break expected (well under the 4.5h continuous-driving
//     limit for Short; Medium may need at most one 45-minute break), so a small fixed pad
//     covers loading/buffer without implying multi-day travel.
//   - LongHaulSingleDriver: real driving time for these pairs (~19-29h) is under a single
//     week's 56h cap on its own, so getting to a genuinely week-long trip relies on the
//     same generous scheduling slack a real shipper would give a multi-day haul, not on
//     forcing an artificial weekly rest. Target the whole span (driving + padding) at
//     roughly 6-8 days - realistic for a shipper who books a transcontinental delivery
//     days in advance rather than expecting next-day arrival - while still including at
//     least one full daily rest (11h) so the rest-compliance engine has something to show.
//   - LongHaulTeamDriver: two drivers relay, so the truck itself barely needs to stop for
//     compliance (~58-62h of real driving is well inside the 90h/2-week cap even alone).
//     The realistic 10-14 day span for these comes overwhelmingly from scheduling slack
//     (the shipper's own generous delivery window for a multi-thousand-km international
//     haul), not from rest - target the whole span at roughly 10-14 days directly.
const double RealisticDailyDrivingHours = 8.5; // ~9h daily cap minus a short break
const double SingleDriverTargetSpanDays = 7.0;
const double TeamDriverTargetSpanDays = 12.0;

(TimeWindow PickupWindow, TimeWindow DeliveryWindow) DeriveWindows(RoutePairFixture pair, DateTime pickupStart)
{
    var pickupEnd = pickupStart.AddHours(2 + random.Next(0, 3)); // 2-4h pickup appointment slot
    var drivingHours = pair.TimeTicks * 5 / 60.0;

    double totalSpanHours;
    switch (pair.Tier)
    {
        case nameof(HaulTier.Short):
            totalSpanHours = drivingHours + 1 + random.NextDouble() * 2; // small buffer only
            break;
        case nameof(HaulTier.Medium):
        {
            var shortBreak = drivingHours > 4.5 ? 0.75 : 0; // one short break at most
            totalSpanHours = drivingHours + shortBreak + 2 + random.NextDouble() * 4; // + loading/buffer
            break;
        }
        case nameof(HaulTier.LongHaulSingleDriver):
        {
            var dailyRestsNeeded = Math.Max(0, Math.Ceiling(drivingHours / RealisticDailyDrivingHours) - 1);
            var minSpanHours = drivingHours + dailyRestsNeeded * 11.0; // at least the driving + mandatory daily rests
            var targetSpanHours = SingleDriverTargetSpanDays * 24 + random.Next(-12, 13); // +/- half a day jitter
            totalSpanHours = Math.Max(minSpanHours, targetSpanHours);
            break;
        }
        case nameof(HaulTier.LongHaulTeamDriver):
        {
            var targetSpanHours = TeamDriverTargetSpanDays * 24 + random.Next(-24, 25); // +/- a day jitter
            totalSpanHours = Math.Max(drivingHours, targetSpanHours);
            break;
        }
        default:
            totalSpanHours = drivingHours + 2;
            break;
    }

    var deliveryStart = pickupEnd.AddHours(totalSpanHours);
    var deliveryEnd = deliveryStart.AddHours(3 + random.Next(0, 6)); // 3-8h delivery appointment slot

    return (TimeWindow.Create(pickupStart, pickupEnd), TimeWindow.Create(deliveryStart, deliveryEnd));
}

// Builds the 3 CorridorOverlap shipments (see LocationPairs.CorridorPairs) as a single
// coordinated set, all the same RequiredTruckType and all bookable onto one truck, with
// windows deliberately overlapping - not independently randomized like DeriveWindows above -
// so that assigning all three to one truck and visiting stops in route order along the
// corridor produces exactly: pick 1, pick 2, drop 2, pick 3, drop 1, drop 3.
//
// corridor-1 (Berlin -> Munich)      pickup opens first,  delivery closes last
// corridor-2 (Leipzig -> Nuremberg)  pickup opens second, delivery closes second (nested inside 1)
// corridor-3 (Nuremberg -> Munich)   pickup opens third,  delivery closes with/after 1
List<ShipmentAggregate> BuildCorridorOverlapShipments(
    DateTime demoAnchor, IReadOnlyList<ShipperAggregate> shippers, TruckType truckType)
{
    var corridor = LocationPairs.CorridorPairs;
    var corridor1 = routePairs.Single(p => p.PairId == corridor[0].PairId); // Berlin -> Munich
    var corridor2 = routePairs.Single(p => p.PairId == corridor[1].PairId); // Leipzig -> Nuremberg
    var corridor3 = routePairs.Single(p => p.PairId == corridor[2].PairId); // Nuremberg -> Munich

    // Anchor the whole set a few days into the demo period so it doesn't collide with the
    // very first tick of the simulation, same spirit as the independently-scattered
    // shipments' own booking spread.
    var corridor1PickupStart = demoAnchor.Date.AddDays(2).AddHours(7);

    // corridor-2's pickup (Leipzig) opens a couple hours after corridor-1's pickup (Berlin) -
    // enough real drive time for the truck to plausibly have reached Leipzig from Berlin.
    var corridor2PickupStart = corridor1PickupStart.AddHours(3);

    // corridor-2's delivery (Nuremberg) closes before corridor-3's pickup (also Nuremberg)
    // opens, but both sit at the same waypoint - drop 2, then pick up 3, in one stop.
    var corridor2DeliveryStart = corridor2PickupStart.AddHours(2 + (corridor2.TimeTicks * 5 / 60.0) + 1);
    var corridor3PickupStart = corridor2DeliveryStart.AddHours(1);

    // corridor-1's delivery (Munich) and corridor-3's delivery (also Munich) both close
    // after corridor-3's pickup, with corridor-3 (the shorter remaining leg) arriving
    // first, corridor-1 (the whole span) closing last.
    var corridor3DeliveryStart = corridor3PickupStart.AddHours(2 + (corridor3.TimeTicks * 5 / 60.0) + 1);
    var corridor1DeliveryStart = corridor3DeliveryStart.AddHours(1);

    ShipmentAggregate MakeCorridorShipment(RoutePairFixture pair, DateTime pickupStart, DateTime deliveryStart)
    {
        var pickupWindow = TimeWindow.Create(pickupStart, pickupStart.AddHours(3));
        var deliveryWindow = TimeWindow.Create(deliveryStart, deliveryStart.AddHours(4));
        var shipper = shippers[random.Next(shippers.Count)];

        // Small load, same rationale as the independently-scattered shipments - leaves
        // capacity for the other two corridor shipments (and anything else) on the same truck.
        var tierCapacity = Capacity.ForTruckSize(TruckSize.Large);
        var fraction = 0.10 + random.NextDouble() * 0.10; // 10-20%, slightly tighter since 3 share one truck
        var load = Capacity.Create(
            Math.Round(tierCapacity.WeightKg * fraction, 0),
            Math.Round(tierCapacity.VolumeCubicMeters * fraction, 1));

        var bookedAt = pickupStart.AddHours(-24) < demoAnchor ? demoAnchor : pickupStart.AddHours(-24);

        return BookFromAddressBook(shipper, pair, load, truckType, pickupWindow, deliveryWindow, bookedAt);
    }

    return
    [
        MakeCorridorShipment(corridor1, corridor1PickupStart, corridor1DeliveryStart),
        MakeCorridorShipment(corridor2, corridor2PickupStart, corridor2DeliveryStart),
        MakeCorridorShipment(corridor3, corridor3PickupStart, corridor3DeliveryStart),
    ];
}

// ---------------------------------------------------------------------------
// Reseed - deletes every row (TRUNCATE ... CASCADE handles FK dependency order
// automatically, so table order doesn't matter) and repopulates exactly four
// things: TruckingCompanies, Shippers, SimulationClock, and Shipments. No
// Trucks/Drivers/Trips this pass.
// ---------------------------------------------------------------------------
async Task Reseed(DateTime demoAnchor)
{
    // Fixed literal table names, not user input - table/column identifiers can't be
    // parameterized anyway (only values can), so the EF1002 SQL-injection analyzer
    // warning doesn't apply here.
#pragma warning disable EF1002
    string[] tables =
    [
        "Shipments", "TripStops", "Trips", "TruckRouteProgresses",
        "DriverComplianceStates", "Trucks", "Drivers", "TruckingCompanies",
        "ShipperAddresses", "Shippers", "SimulationClock", "DeviceTokens",
    ];
    foreach (var table in tables)
    {
        await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{table}\" CASCADE;");
    }
#pragma warning restore EF1002
    Console.WriteLine("Cleared all data.");

    // 1. TruckingCompanies (5) - each named after the river of its office city, with a fixed
    // office at one of the fixture's named places: one company per city, never random.
    (string Name, string OfficePlace)[] companyDefinitions =
    [
        ("Rheinfracht Logistik AG", "Cologne-Ehrenfeld"),
        ("Elbstrom Transporte KG", "Hamburg-Altona"),
        ("Donau-Trans Spedition", "Vienna-Favoriten"),
        ("Moldau Spedition s.r.o.", "Prague-Vinohrady"),
        ("Rhône Fret Logistique SA", "Lyon-Part-Dieu"),
    ];

    var companyByOffice = companyDefinitions.ToDictionary(
        company => company.OfficePlace,
        company => TruckingCompany.Create(
            Guid.NewGuid(), company.Name, PlaceLocation(company.OfficePlace), PlaceArea(company.OfficePlace)));
    var companies = companyByOffice.Values.ToList();
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

    // 3. SimulationClock - the single global "now" for the whole system, set to the
    // configured anchor rather than real time.
    var clock = SimulationClock.Create(demoAnchor);
    db.Add(clock);
    Console.WriteLine($"SimulationClock set to {demoAnchor:yyyy-MM-dd HH:mm} UTC.");

    // 4. Shipments - four groups:
    //   - the 7-day team demo loop: 12 small Flatbed shipments booked directly to the Cologne
    //     company (the demo's main company, mid-map), with fixed windows (see below),
    //   - 5 regional ones booked directly to the Cologne company,
    //   - 3-4 booked directly to each other company, starting near its own office,
    //   - 8 open ones any company can send offers for: 5 below plus the corridor set (step 5).
    // Outside the loop the truck type rotates through every group so each type has work.
    // Direct shipments pick up within the first four days. Open ones are spread over two
    // weeks, each booked 12-48h before its pickup (never before the anchor), so their 2-hour
    // offer windows close at different times as the clock moves.
    const int DirectPickupSpreadDays = 4;
    const int OpenPickupSpreadDays = 14;

    var truckTypes = Enum.GetValues<TruckType>();
    var shipments = new List<ShipmentAggregate>();

    void AddShipment(string pairId, TruckingCompany? directCompany)
    {
        var routePair = RoutePair(pairId);
        var spreadDays = directCompany is null ? OpenPickupSpreadDays : DirectPickupSpreadDays;

        // A pickup appointment somewhere in a 06:00-18:00 day, never before the anchor itself.
        var pickupStart = demoAnchor.Date
            .AddDays(random.Next(0, spreadDays))
            .AddHours(6 + random.NextDouble() * 12);
        if (pickupStart < demoAnchor.AddHours(1))
        {
            pickupStart = demoAnchor.AddHours(1);
        }

        // Only an open shipment's booking time matters: it starts the 2-hour offer window.
        var bookedAt = directCompany is null ? pickupStart.AddHours(-(12 + random.NextDouble() * 36)) : demoAnchor;
        if (bookedAt < demoAnchor)
        {
            bookedAt = demoAnchor;
        }

        var (pickupWindow, deliveryWindow) = DeriveWindows(routePair, pickupStart);

        // Kept deliberately small (10-25% of a tier's capacity) - these shipments are meant to
        // become stops added onto a truck's route alongside others, so each one should leave
        // plenty of remaining capacity for further stops to be added later.
        var tierCapacity = Capacity.ForTruckSize(RandomTruckSize());
        var fraction = 0.10 + random.NextDouble() * 0.15;
        var load = Capacity.Create(
            Math.Round(tierCapacity.WeightKg * fraction, 0),
            Math.Round(tierCapacity.VolumeCubicMeters * fraction, 1));

        shipments.Add(BookFromAddressBook(
            shippers[random.Next(shippers.Count)],
            routePair,
            load,
            truckTypes[shipments.Count % truckTypes.Length],
            pickupWindow,
            deliveryWindow,
            bookedAt,
            directCompany?.Id));
    }

    (string OfficePlace, string[] PairIds)[] directShipments =
    [
        ("Cologne-Ehrenfeld",
        [
            "short-cologne-duesseldorf", "short-bonn-essen", "short-aachen-cologne", "short-cologne-dortmund",
            "short-cologne-frankfurt",
        ]),
        ("Hamburg-Altona", ["short-hamburg-hannover", "short-hamburg-bremen", "short-kiel-hamburg"]),
        ("Vienna-Favoriten", ["short-vienna-graz", "short-linz-vienna", "short-vienna-bratislava"]),
        ("Prague-Vinohrady", ["short-prague-brno", "short-prague-dresden", "short-pilsen-prague"]),
        ("Lyon-Part-Dieu", ["short-lyon-grenoble", "medium-lyon-marseille", "short-geneva-lyon", "short-lyon-grenoble"]),
    ];

    foreach (var (officePlace, pairIds) in directShipments)
    {
        foreach (var pairId in pairIds)
        {
            AddShipment(pairId, companyByOffice[officePlace]);
        }
    }

    // The 7-day team demo loop: one Large Flatbed with two drivers, leaving the Cologne office
    // on the anchor day (a Saturday) and back on day 5 (Thursday), before the six-day mark, so
    // no weekly rest falls in the trip. Day 0 is packed: about 15h of driving between the
    // windows, which only a team can do (a single driver may drive 9-10h a day), so the same
    // assignment fails on a single-driver truck. From day 1 the windows are daytime slots and
    // the truck parks overnight at the next stop; those waits count as the drivers' rest.
    // (Day offset from the anchor's date, window opens, window closes), for pickup then delivery.
    var loopCompany = companyByOffice["Cologne-Ehrenfeld"];
    ((int Day, string From, string To) Pickup, (int Day, string From, string To) Delivery, double WeightKg, double VolumeCubicMeters)[] teamLoopWindows =
    [
        ((0, "07:30", "10:00"), (0, "11:00", "15:00"), 4000, 12), // Brussels -> Paris
        ((0, "09:00", "12:00"), (0, "15:00", "19:00"), 3000, 10), // Lille -> Metz
        ((0, "13:00", "17:00"), (0, "18:30", "22:30"), 5000, 15), // Reims -> Basel
        ((0, "17:00", "21:00"), (1, "08:00", "13:00"), 2500, 8),  // Strasbourg -> Innsbruck
        ((0, "19:30", "23:55"), (1, "13:00", "17:00"), 6000, 18), // Zurich -> Salzburg
        ((1, "11:00", "15:00"), (2, "10:00", "14:00"), 3500, 11), // Munich -> Vienna
        ((2, "08:00", "12:00"), (3, "08:00", "12:00"), 4500, 14), // Linz -> Prague
        ((2, "12:00", "16:00"), (3, "11:00", "15:00"), 2000, 6),  // Brno -> Leipzig
        ((3, "09:30", "13:30"), (4, "09:30", "13:30"), 5500, 16), // Dresden -> Magdeburg
        ((4, "08:00", "12:00"), (5, "08:00", "12:00"), 3000, 9),  // Berlin -> Hamburg
        ((4, "11:30", "15:30"), (5, "11:00", "15:00"), 4000, 12), // Hannover -> Münster
        ((5, "09:00", "13:00"), (5, "12:00", "16:00"), 2500, 7),  // Bremen -> Dortmund
    ];

    DateTime OnDay(int day, string time) => demoAnchor.Date.AddDays(day).Add(TimeSpan.Parse(time));

    var teamLoopPairs = LocationPairs.TeamLoopPairs;
    for (var i = 0; i < teamLoopPairs.Count; i++)
    {
        var routePair = RoutePair(teamLoopPairs[i].PairId);
        var (pickup, delivery, weightKg, volumeCubicMeters) = teamLoopWindows[i];
        shipments.Add(BookFromAddressBook(
            shippers[random.Next(shippers.Count)],
            routePair,
            Capacity.Create(weightKg, volumeCubicMeters),
            TruckType.Flatbed,
            TimeWindow.Create(OnDay(pickup.Day, pickup.From), OnDay(pickup.Day, pickup.To)),
            TimeWindow.Create(OnDay(delivery.Day, delivery.From), OnDay(delivery.Day, delivery.To)),
            demoAnchor,
            loopCompany.Id));
    }

    var directCount = shipments.Count;

    // Open shipments across Germany and Europe, including the 7-day and 10-14-day long hauls.
    string[] openPairIds =
    [
        "short-berlin-leipzig", "short-munich-nuremberg", "medium-frankfurt-warsaw",
        "long-berlin-lisbon", "team-berlin-astana",
    ];
    foreach (var pairId in openPairIds)
    {
        AddShipment(pairId, directCompany: null);
    }

    // 5. Corridor-overlap set - 3 shipments along one shared real corridor (Berlin ->
    // Leipzig -> Nuremberg -> Munich), deliberately overlapping so a dispatcher can build
    // an interleaved route on a single truck: pick 1, pick 2, drop 2, pick 3, drop 1, drop
    // 3. All three share RequiredTruckType = Flatbed so one truck is eligible for all of
    // them. See BuildCorridorOverlapShipments above.
    var corridorShipments = BuildCorridorOverlapShipments(demoAnchor, shippers, TruckType.Flatbed);
    shipments.AddRange(corridorShipments);
    Console.WriteLine($"Generated {corridorShipments.Count} corridor-overlap shipments (Berlin-Leipzig-Nuremberg-Munich).");

    db.AddRange(shipments);
    Console.WriteLine($"Generated {shipments.Count} shipments total ({directCount} direct, {shipments.Count - directCount} open).");

    // 6. Every shipper has at least two saved addresses to book from - shippers with fewer
    // (no or few seeded shipments) get extra fixture places.
    var placeNames = routePairs
        .SelectMany(pair => new[] { pair.PickupName, pair.DeliveryName })
        .Distinct()
        .Order(StringComparer.Ordinal)
        .ToList();
    foreach (var shipper in shippers)
    {
        while (shipper.Addresses.Count < 2)
        {
            SavedAddress(shipper, placeNames[addressRandom.Next(placeNames.Count)], demoAnchor);
        }
    }

    var addressCount = shippers.Sum(shipper => shipper.Addresses.Count);
    Console.WriteLine($"Generated {addressCount} saved addresses.");

    await db.SaveChangesAsync();

    Console.WriteLine("Seed data committed.");
    Console.WriteLine($"  TruckingCompanies: {companies.Count}");
    Console.WriteLine($"  Shippers: {shippers.Count} ({addressCount} saved addresses)");
    Console.WriteLine($"  Shipments: {shipments.Count}");
}

await Reseed(anchorUtc);
