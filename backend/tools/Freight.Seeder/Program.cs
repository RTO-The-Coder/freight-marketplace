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
// every pair in LocationPairs.All and writes Fixtures/route-pairs.json. The ordinary seed
// run below never takes this path and never calls OSRM itself - see Fixtures/FixtureBuilder.cs.
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

var pairsByTier = routePairs.ToLookup(p => p.Tier);

// Weighted draw across haul tiers for "realistic variety": mostly short/medium regional
// jobs, with a meaningful minority of long-haul pairs so the 7-day (single-driver) and
// 10-14-day (team-driver) trip scenarios are always represented once shipments are
// assigned to a real fleet. CorridorOverlap pairs are deliberately excluded here - they're
// seeded as their own coordinated set (see SeedCorridorOverlapShipments below), not picked
// independently, since their whole point is to share compatible, overlapping windows.
RoutePairFixture RandomRoutePair()
{
    var roll = random.NextDouble();
    var tier = roll switch
    {
        < 0.35 => nameof(HaulTier.Short),
        < 0.65 => nameof(HaulTier.Medium),
        < 0.90 => nameof(HaulTier.LongHaulSingleDriver),
        _ => nameof(HaulTier.LongHaulTeamDriver),
    };

    // Fall back to any non-corridor tier if the chosen one has no fixture entries (e.g. all
    // its OSRM calls failed when the fixture was built) rather than crashing the whole run.
    var candidates = pairsByTier[tier].ToList();
    if (candidates.Count == 0)
    {
        candidates = [.. routePairs.Where(p => p.Tier != nameof(HaulTier.CorridorOverlap))];
    }

    return candidates[random.Next(candidates.Count)];
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

        return ShipmentAggregate.Book(
            shipper.Id,
            GeoLocation.Create(pair.PickupLat, pair.PickupLon),
            GeoLocation.Create(pair.DeliveryLat, pair.DeliveryLon),
            load,
            truckType,
            pickupWindow,
            deliveryWindow,
            bookedAt);
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
        "Shippers", "SimulationClock",
    ];
    foreach (var table in tables)
    {
        await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{table}\" CASCADE;");
    }
#pragma warning restore EF1002
    Console.WriteLine("Cleared all data.");

    // 1. TruckingCompanies (5) - each with a real office location (drawn from the fixture's
    // pickup/delivery place list so office coordinates stay consistent with the same
    // real-place convention used everywhere else).
    string[] companyNames =
    [
        "Nordwest Spedition GmbH",
        "Rheinfracht Logistik AG",
        "Elbstrom Transporte KG",
        "Süddeutsche Fernfracht GmbH",
        "Alpen-Trans Spedition",
    ];

    GeoLocation RandomOfficeLocation()
    {
        var pair = routePairs[random.Next(routePairs.Count)];
        return random.Next(2) == 0
            ? GeoLocation.Create(pair.PickupLat, pair.PickupLon)
            : GeoLocation.Create(pair.DeliveryLat, pair.DeliveryLon);
    }

    var companies = companyNames
        .Select(name => TruckingCompany.Create(Guid.NewGuid(), name, RandomOfficeLocation()))
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

    // 3. SimulationClock - the single global "now" for the whole system, set to the
    // configured anchor rather than real time.
    var clock = SimulationClock.Create(demoAnchor);
    db.Add(clock);
    Console.WriteLine($"SimulationClock set to {demoAnchor:yyyy-MM-dd HH:mm} UTC.");

    // 4. Shipments - 20 total, 5 per TruckType (BoxVan, Flatbed, Refrigerated,
    // Tanker). Booked at a random point across a 14-day booking window starting at
    // demoAnchor (not all clustered on the anchor's own calendar date), each on a
    // random hour of its own booking day - so a demo that advances the simulation
    // clock keeps finding freshly-booked shipments over roughly the fleet's own
    // 7-14 day trip horizon, rather than all 20 shipments' pickups landing within the
    // first day or two. All still relative to demoAnchor (the same value just written to
    // SimulationClock), not real time. Booked via Shipment.Book(...) - all start Pending,
    // no TruckingCompanyId, exactly as a real Shipper submission would. Each shipment's
    // pickup/delivery locations and windows now come from a real route-pair fixture entry
    // (see DeriveWindows above) instead of independently-random locations + guessed hours.
    const int BookingSpreadDays = 14;

    var shipments = new List<ShipmentAggregate>();
    foreach (TruckType truckType in Enum.GetValues<TruckType>())
    {
        for (var i = 0; i < 5; i++)
        {
            // Each shipment's pickup starts on its own random day within the spread
            // window, at its own random hour - so shipments land throughout the whole
            // demo period instead of clustering near the anchor date.
            var pickupStart = demoAnchor.Date
                .AddDays(random.Next(0, BookingSpreadDays))
                .AddHours(6 + random.NextDouble() * 12); // pickup appointment somewhere in a 06:00-18:00 day

            // A real Shipper books a day or so before the pickup window opens, not weeks in
            // advance - bookedAt only drives OfferDeadline (30 min after booking), so this
            // just needs to be a plausible moment shortly before pickupStart. Never earlier
            // than demoAnchor itself, since the simulated world doesn't exist before that.
            var bookedAt = pickupStart.AddHours(-(12 + random.NextDouble() * 36));
            if (bookedAt < demoAnchor)
            {
                bookedAt = demoAnchor;
            }
            var shipper = shippers[random.Next(shippers.Count)];
            var routePair = RandomRoutePair();
            var (pickupWindow, deliveryWindow) = DeriveWindows(routePair, pickupStart);

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

            var pickupLocation = GeoLocation.Create(routePair.PickupLat, routePair.PickupLon);
            var deliveryLocation = GeoLocation.Create(routePair.DeliveryLat, routePair.DeliveryLon);

            var shipment = ShipmentAggregate.Book(
                shipper.Id,
                pickupLocation,
                deliveryLocation,
                load,
                truckType,
                pickupWindow,
                deliveryWindow,
                bookedAt);

            shipments.Add(shipment);
        }
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
    Console.WriteLine($"Generated {shipments.Count} shipments total (20 scattered + {corridorShipments.Count} corridor-overlap).");

    await db.SaveChangesAsync();

    Console.WriteLine("Seed data committed.");
    Console.WriteLine($"  TruckingCompanies: {companies.Count}");
    Console.WriteLine($"  Shippers: {shippers.Count}");
    Console.WriteLine($"  Shipments: {shipments.Count}");
}

await Reseed(anchorUtc);
