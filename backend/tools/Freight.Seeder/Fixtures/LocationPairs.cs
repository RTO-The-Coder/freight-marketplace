namespace Freight.Seeder.Fixtures;

/// <summary>
/// The curated set of pickup/delivery location pairs the seeder draws Shipments from.
/// Distances are real road distances (see <see cref="RoutePairFixture"/>), not straight-line
/// guesses - this file only defines which named places pair up and what haul tier they
/// target; <c>route-pairs.json</c> (built once via <c>--build-fixture</c>) holds the actual
/// OSRM-computed distance/time for each pair.
///
/// Every place is a named suburb/district, never a city centroid - including suburbs of
/// smaller towns, not just the well-known districts of major metros - matching the
/// project's "real, named, suburb-level locations only" geography convention.
/// </summary>
public enum HaulTier
{
    /// <summary>~50-150km - regional, same-day jobs.</summary>
    Short,

    /// <summary>~300-800km - a long single day or an overnight, no rest-rule drama.</summary>
    Medium,

    /// <summary>
    /// ~2,000-4,000km - sized so a single-driver Truck's weekly cap (56h driving, EU rest
    /// rules) makes this roughly a 7-day trip end to end, once assigned to a real Truck.
    /// </summary>
    LongHaulSingleDriver,

    /// <summary>
    /// ~6,500-9,000km - long enough that a two-driver (team) Truck relaying against the
    /// 90h/2-week cap is genuinely needed; a single driver could not cover this distance in
    /// 10-14 days. Only reachable by road (OSRM has no ferry/sea-crossing modeling), so pairs
    /// here must be continuous overland routes.
    /// </summary>
    LongHaulTeamDriver,

    /// <summary>
    /// One of several shipments sharing a single real corridor (see
    /// <see cref="LocationPairs.CorridorPairs"/>) - their pickup/delivery points are
    /// waypoints along the same road, chosen so their spans genuinely overlap (one
    /// shipment's delivery point sits between another's pickup and delivery). Lets a
    /// dispatcher build an interleaved route on one truck - e.g. pick up shipment 1, pick up
    /// shipment 2, drop shipment 2, pick up shipment 3, drop shipment 1, drop shipment 3 -
    /// instead of only ever assigning independent point-to-point shipments back to back.
    /// </summary>
    CorridorOverlap,
}

public sealed record NamedPlace(string Name, double Lat, double Lon);

public sealed record LocationPairDefinition(string PairId, NamedPlace Pickup, NamedPlace Delivery, HaulTier Tier);

public static class LocationPairs
{
    // Real, named suburbs/districts - never a bare city name or a street address. Includes
    // suburbs of small/mid-size towns (e.g. Fürth-Poppenreuth, Erfurt-Ilversgehofen), not
    // only districts of large metros, per the project's geography convention.
    private static readonly NamedPlace BerlinMitte = new("Berlin-Mitte", 52.5200, 13.4050);
    private static readonly NamedPlace LeipzigPlagwitz = new("Leipzig-Plagwitz", 51.3300, 12.3300);
    private static readonly NamedPlace MunichSchwabing = new("Munich-Schwabing", 48.1642, 11.5822);
    private static readonly NamedPlace NurembergGostenhof = new("Nuremberg-Gostenhof", 49.4480, 11.0500);
    private static readonly NamedPlace HamburgAltona = new("Hamburg-Altona", 53.5511, 9.9349);
    private static readonly NamedPlace HannoverLinden = new("Hannover-Linden", 52.3700, 9.6900);
    private static readonly NamedPlace CologneEhrenfeld = new("Cologne-Ehrenfeld", 50.9540, 6.9200);
    private static readonly NamedPlace FrankfurtSachsenhausen = new("Frankfurt-Sachsenhausen", 50.1010, 8.6821);
    private static readonly NamedPlace FurthPoppenreuth = new("Fürth-Poppenreuth", 49.4980, 10.9720);
    private static readonly NamedPlace ErfurtIlversgehofen = new("Erfurt-Ilversgehofen", 51.0080, 11.0270);
    private static readonly NamedPlace GoettingenGrone = new("Göttingen-Grone", 51.5390, 9.8850);
    private static readonly NamedPlace HildesheimDrispenstedt = new("Hildesheim-Drispenstedt", 52.1620, 9.9280);

    private static readonly NamedPlace ViennaFavoriten = new("Vienna-Favoriten", 48.1720, 16.3820);
    private static readonly NamedPlace MilanNavigli = new("Milan-Navigli", 45.4520, 9.1730);
    private static readonly NamedPlace WarsawPraga = new("Warsaw-Praga", 52.2530, 21.0410);
    private static readonly NamedPlace PragueVinohrady = new("Prague-Vinohrady", 50.0755, 14.4378);
    private static readonly NamedPlace BrnoZidenice = new("Brno-Židenice", 49.2020, 16.6480);

    private static readonly NamedPlace LisbonAlfama = new("Lisbon-Alfama", 38.7139, -9.1288);
    private static readonly NamedPlace AthensPlaka = new("Athens-Plaka", 37.9755, 23.7348);
    private static readonly NamedPlace TallinnKesklinn = new("Tallinn-Kesklinn", 59.4370, 24.7536);
    private static readonly NamedPlace MadridVallecas = new("Madrid-Vallecas", 40.3880, -3.6520);
    private static readonly NamedPlace CoimbraSantaClara = new("Coimbra-Santa Clara", 40.1980, -8.4300);

    // Very-long-haul (team-driver) endpoints - continuous overland points reachable by road
    // from Western/Central Europe without a sea crossing.
    private static readonly NamedPlace AstanaEsil = new("Astana-Esil", 51.1605, 71.4704);
    private static readonly NamedPlace TbilisiOldTown = new("Tbilisi-Old Town", 41.6934, 44.8015);

    public static readonly IReadOnlyList<LocationPairDefinition> All =
    [
        // Short-haul (~50-150km) - includes small-town suburbs, not just major-metro districts
        new("short-berlin-leipzig", BerlinMitte, LeipzigPlagwitz, HaulTier.Short),
        new("short-munich-nuremberg", MunichSchwabing, NurembergGostenhof, HaulTier.Short),
        new("short-hamburg-hannover", HamburgAltona, HannoverLinden, HaulTier.Short),
        new("short-cologne-frankfurt", CologneEhrenfeld, FrankfurtSachsenhausen, HaulTier.Short),
        new("short-nuremberg-furth", NurembergGostenhof, FurthPoppenreuth, HaulTier.Short),
        new("short-hannover-hildesheim", HannoverLinden, HildesheimDrispenstedt, HaulTier.Short),
        new("short-goettingen-hildesheim", GoettingenGrone, HildesheimDrispenstedt, HaulTier.Short),
        new("short-leipzig-erfurt", LeipzigPlagwitz, ErfurtIlversgehofen, HaulTier.Short),

        // Medium-haul (~300-800km)
        new("medium-berlin-munich", BerlinMitte, MunichSchwabing, HaulTier.Medium),
        new("medium-cologne-vienna", CologneEhrenfeld, ViennaFavoriten, HaulTier.Medium),
        new("medium-hamburg-milan", HamburgAltona, MilanNavigli, HaulTier.Medium),
        new("medium-frankfurt-warsaw", FrankfurtSachsenhausen, WarsawPraga, HaulTier.Medium),
        new("medium-munich-prague", MunichSchwabing, PragueVinohrady, HaulTier.Medium),
        new("medium-erfurt-brno", ErfurtIlversgehofen, BrnoZidenice, HaulTier.Medium),

        // Long-haul, single-driver ~7-day trip target (~2,000-4,000km)
        new("long-berlin-lisbon", BerlinMitte, LisbonAlfama, HaulTier.LongHaulSingleDriver),
        new("long-hamburg-athens", HamburgAltona, AthensPlaka, HaulTier.LongHaulSingleDriver),
        new("long-munich-tallinn", MunichSchwabing, TallinnKesklinn, HaulTier.LongHaulSingleDriver),
        new("long-frankfurt-madrid", FrankfurtSachsenhausen, MadridVallecas, HaulTier.LongHaulSingleDriver),
        new("long-hildesheim-coimbra", HildesheimDrispenstedt, CoimbraSantaClara, HaulTier.LongHaulSingleDriver),

        // Very-long-haul, team-driver ~10-14 day trip target (~6,500-9,000km)
        new("team-berlin-astana", BerlinMitte, AstanaEsil, HaulTier.LongHaulTeamDriver),
        new("team-lisbon-tbilisi", LisbonAlfama, TbilisiOldTown, HaulTier.LongHaulTeamDriver),

        // Corridor overlap set - three shipments along the real Berlin -> Leipzig ->
        // Nuremberg -> Munich road corridor (all four places already defined above),
        // deliberately overlapping so a single truck's route naturally interleaves them:
        //   corridor-1 (Berlin -> Munich)   spans the whole corridor
        //   corridor-2 (Leipzig -> Nuremberg) nests entirely inside corridor-1's span
        //   corridor-3 (Nuremberg -> Munich)  starts where corridor-2 ends, finishes with corridor-1
        // Visiting pickups/deliveries in route order along the corridor gives exactly:
        // pick 1, pick 2, drop 2, pick 3, drop 1, drop 3.
        new("corridor-1-berlin-munich", BerlinMitte, MunichSchwabing, HaulTier.CorridorOverlap),
        new("corridor-2-leipzig-nuremberg", LeipzigPlagwitz, NurembergGostenhof, HaulTier.CorridorOverlap),
        new("corridor-3-nuremberg-munich", NurembergGostenhof, MunichSchwabing, HaulTier.CorridorOverlap),
    ];

    /// <summary>
    /// The <see cref="HaulTier.CorridorOverlap"/> entries from <see cref="All"/>, in the
    /// fixed route order (corridor-1, corridor-2, corridor-3) the seeder uses to guarantee
    /// all three are seeded together with a compatible <see cref="Fixtures.HaulTier"/>-aware
    /// window (see Program.cs's DeriveWindows) - unlike the other tiers, these are drawn as a
    /// deliberate SET, not an independent random pick per shipment.
    /// </summary>
    public static IReadOnlyList<LocationPairDefinition> CorridorPairs =>
        All.Where(p => p.Tier == HaulTier.CorridorOverlap).ToList();
}
