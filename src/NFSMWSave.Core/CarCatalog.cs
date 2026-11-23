using System.Reflection;
using System.Text;

namespace NFSMWSave.Core;

public enum VehicleGroup
{
    Stock,
    Bosses,
    Presets,
    Cops,
    Traffic,
    Other,
}

public enum VehicleSafety
{
    Drivable,
    AiVehicle,
    MayCrash,
}

public sealed record CatalogVehicle(string Id, uint VehicleKey, uint FeKey, string Name, VehicleGroup Group, VehicleSafety Safety, ushort[] Parts)
{
    public uint PresetHash { get; init; }
    public int[]? Levels { get; init; }
    public int Order { get; init; }
}

public sealed record LotChoice(CatalogVehicle Vehicle, bool InLot);

public sealed class CatalogFamily
{
    internal CatalogFamily(string label, byte[] partIds, List<CatalogRow> rows)
    {
        Label = label;
        PartIds = partIds;
        Rows = rows;
    }

    public string Label { get; }
    internal byte[] PartIds { get; }
    internal List<CatalogRow> Rows { get; }
}

internal sealed record CatalogRow(string Id, int Usage, ushort[] Parts);

public static class CarCatalog
{
    private const int UsageCop = 1;
    private const int UsageTraffic = 2;
    private const ushort NoPart = 0xFFFF;
    private const int MaxWrongPercent = 1;
    private const string GenericFrontend = "vehicles";

    private static readonly Dictionary<string, string> FrontendKeys = new()
    {
        ["a3"] = "a3_20t",
        ["a4"] = "a4_32",
        ["carreragt"] = "carrera_gt",
        ["corvette"] = "corvette_car",
        ["imprezawrx"] = "imprezzawrx",
        ["bmwm3gtr"] = "m3_gtr",
        ["bmwm3gtre46"] = "m3_gtre46",
    };

    private static readonly HashSet<string> ProvenVariants =
    [
        "cs_c6_copsporthench", "cs_gto_copgto", "cs_viper_copmidsize", "cs_mustang_copsuv", "copcross",
        "cs_clio_trafpizza", "cs_clio_traftaxi", "cs_cts_traf_minivan", "cs_cts_traffictruck", "cs_trafcement", "cs_trafgarb",
        "mustang_demo", "speedtest",
    ];

    private static readonly Dictionary<string, string> Names = new()
    {
        ["copmidsize"] = "Police Cruiser",
        ["copghost"] = "Undercover Cruiser",
        ["copgto"] = "Police GTO",
        ["copgtoghost"] = "Undercover GTO",
        ["copsport"] = "Police Corvette",
        ["copsportghost"] = "Undercover Corvette",
        ["copsporthench"] = "Police Corvette (Hench)",
        ["copsuv"] = "Police SUV",
        ["copsuvl"] = "Police SUV (Light)",
        ["copheli"] = "Police Helicopter",
        ["copm3gtr"] = "Police M3 GTR",
        ["copm3gtrghost"] = "Undercover M3 GTR",
        ["copgall"] = "Police Gallardo",
        ["copgallghost"] = "Undercover Gallardo",
        ["copcross"] = "Sgt. Cross's Corvette",
        ["cs_c6_copsporthench"] = "Police Corvette (Challenge)",
        ["cs_gto_copgto"] = "Police GTO (Challenge)",
        ["cs_viper_copmidsize"] = "Police Cruiser (Challenge)",
        ["cs_mustang_copsuv"] = "Police SUV (Challenge)",
        ["cs_clio_trafpizza"] = "Pizza Van (Challenge)",
        ["cs_clio_traftaxi"] = "Taxi (Challenge)",
        ["cs_cts_traf_minivan"] = "Minivan (Challenge)",
        ["cs_cts_traffictruck"] = "Pickup Truck (Challenge)",
        ["cs_trafcement"] = "Cement Truck (Challenge)",
        ["cs_trafgarb"] = "Garbage Truck (Challenge)",
        ["semi"] = "Semi Truck",
        ["traf4dseda"] = "Sedan A",
        ["traf4dsedb"] = "Sedan B",
        ["traf4dsedc"] = "Sedan C",
        ["trafamb"] = "Ambulance",
        ["trafcamper"] = "Camper",
        ["trafcemtr"] = "Cement Truck",
        ["trafcourt"] = "Courier Van",
        ["trafdmptr"] = "Dump Truck",
        ["trafficcoup"] = "Coupe",
        ["traffire"] = "Fire Truck",
        ["trafgarb"] = "Garbage Truck",
        ["trafha"] = "Hatchback",
        ["trafminivan"] = "Minivan",
        ["trafnews"] = "News Van",
        ["trafpickupa"] = "Pickup Truck",
        ["trafpizza"] = "Pizza Van",
        ["trafstwag"] = "Station Wagon",
        ["trafsuva"] = "SUV",
        ["taxi"] = "Taxi",
        ["traftaxi"] = "Taxi",
        ["trafvanb"] = "Van",
        ["trailera"] = "Trailer A",
        ["trailerb"] = "Trailer B",
        ["trailercmt"] = "Cement Trailer",
        ["trailercon"] = "Container Trailer",
        ["trailercrate"] = "Crate Trailer",
        ["trailerlog"] = "Log Trailer",
        ["rx8speedt"] = "Mazda RX-8 (Speed Team)",
        ["mustang_demo"] = "Ford Mustang GT (Demo)",
        ["speedtest"] = "BMW M3 GTR (Speed Test)",
    };

    private static readonly Dictionary<uint, string> NamesByKey = Names.ToDictionary(n => AttribHash.Of(n.Key), n => n.Value);
    private static readonly Lazy<(byte[] Slots, List<CatalogFamily> Families)> Data = new(Load);

    public static IReadOnlyList<CatalogFamily> Families => Data.Value.Families;

    internal static uint GenericFrontendKey { get; } = AttribHash.Of(GenericFrontend);

    internal const string PrologueName = "BMW M3 GTR (Prologue)";

    public static uint PrologueKey { get; } = AttribHash.Of("m3gtre46careerstart");

    private static readonly uint PrologueBaseKey = AttribHash.Of("bmwm3gtre46");

    public static uint PartsKey(uint vehicleKey) => vehicleKey == PrologueKey ? PrologueBaseKey : vehicleKey;

    private static readonly HashSet<uint> AddonKeys = new[] { "copgall", "copgallghost", "copm3gtr", "copm3gtrghost" }.Select(id => AttribHash.Of(id)).ToHashSet();

    internal static bool IsAddon(uint vehicleKey) => AddonKeys.Contains(vehicleKey);

    public static string? VehicleName(uint vehicleKey) => NamesByKey.GetValueOrDefault(vehicleKey);

    private static readonly Lazy<Dictionary<uint, string>> StockNames = new(() => Data.Value.Families
        .SelectMany(f => f.Rows)
        .Where(r => r.Usage == 0)
        .Select(r => (Key: AttribHash.Of(r.Id), Info: GameData.FindCar(AttribHash.Of(FrontendKeys.GetValueOrDefault(r.Id, r.Id)))))
        .Where(r => r.Info is not null)
        .GroupBy(r => r.Key)
        .ToDictionary(g => g.Key, g => g.First().Info!.Name));

    private static readonly HashSet<uint> ChallengeVariantKeys = Names.Keys.Where(id => id.StartsWith("cs_", StringComparison.Ordinal)).Select(id => AttribHash.Of(id)).ToHashSet();

    internal static bool IsChallengeCar(uint vehicleKey, uint frontendKey) =>
        frontendKey == GenericFrontendKey && (ChallengeVariantKeys.Contains(vehicleKey) || StockNames.Value.ContainsKey(vehicleKey));

    internal static string? ChallengeName(uint vehicleKey, uint frontendKey) =>
        frontendKey == GenericFrontendKey && StockNames.Value.TryGetValue(vehicleKey, out var name) ? $"{name} (Challenge)" : null;

    public static CatalogFamily? Choose(IEnumerable<ushort[]> records)
    {
        var list = records.ToList();
        var (slots, families) = Data.Value;
        CatalogFamily? best = null;
        var bestWrong = int.MaxValue;
        var bestGood = 0;
        foreach (var family in families)
        {
            var good = 0;
            var wrong = 0;
            foreach (var parts in list)
            {
                for (var slot = 0; slot < parts.Length; slot++)
                {
                    var part = parts[slot];
                    if (part == NoPart)
                    {
                        continue;
                    }
                    if (part < family.PartIds.Length && family.PartIds[part] == slots[slot])
                    {
                        good++;
                    }
                    else
                    {
                        wrong++;
                    }
                }
            }
            if (wrong < bestWrong)
            {
                best = family;
                bestWrong = wrong;
                bestGood = good;
            }
        }
        return best is not null && bestWrong * 100 <= bestGood * MaxWrongPercent ? best : null;
    }

    internal static bool PartFits(CatalogFamily family, ushort part, int slot) => part < family.PartIds.Length && family.PartIds[part] == Data.Value.Slots[slot];

    public static IReadOnlyList<CatalogVehicle> Vehicles(CatalogFamily family)
    {
        var vehicles = new List<CatalogVehicle>();
        foreach (var row in family.Rows)
        {
            var frontend = AttribHash.Of(FrontendKeys.GetValueOrDefault(row.Id, row.Id));
            var info = row.Usage == 0 ? GameData.FindCar(frontend) : null;
            var feKey = info is null ? AttribHash.Of(GenericFrontend) : frontend;
            var name = info?.Name ?? Names.GetValueOrDefault(row.Id) ?? row.Id.ToUpperInvariant();
            vehicles.Add(new CatalogVehicle(row.Id, AttribHash.Of(row.Id), feKey, name, GroupOf(row, info), SafetyOf(row, info), row.Parts));
        }
        return vehicles.OrderBy(v => v.Group).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<CatalogVehicle> WithPresets(CatalogFamily family)
    {
        var vehicles = Vehicles(family);
        var byKey = vehicles.GroupBy(v => v.VehicleKey).ToDictionary(g => g.Key, g => g.First());
        var presets = new List<CatalogVehicle>();
        foreach (var row in CarPresets.For(family.Label))
        {
            if (!byKey.TryGetValue(PartsKey(row.VehicleKey), out var car) || row.Parts[CarParts.BodySlot] == NoPart || !PresetFits(family, row))
            {
                continue;
            }
            var rank = CarPresets.BossRank(row.Name);
            presets.Add(new CatalogVehicle(row.Name, row.VehicleKey, row.FeKey, CarPresets.Title(row.Name, car.Name), rank > 0 ? VehicleGroup.Bosses : VehicleGroup.Presets, car.Safety, row.Parts)
            {
                PresetHash = row.Hash,
                Levels = row.Levels,
                Order = rank,
            });
        }
        return vehicles.Concat(presets).Where(v => !IsAddon(v.VehicleKey)).OrderBy(v => v.Group).ThenBy(v => v.Order).ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool PresetFits(CatalogFamily family, PresetRow row)
    {
        for (var slot = 0; slot < row.Parts.Length; slot++)
        {
            if (row.Parts[slot] != NoPart && !PartFits(family, row.Parts[slot], slot))
            {
                return false;
            }
        }
        return true;
    }

    private static VehicleGroup GroupOf(CatalogRow row, CarInfo? info)
    {
        if (info is not null)
        {
            return VehicleGroup.Stock;
        }
        if (IsUnsafe(row.Id) || row.Id is "mustang_demo" or "speedtest" or "rx8speedt")
        {
            return VehicleGroup.Other;
        }
        if (row.Usage == UsageCop)
        {
            return VehicleGroup.Cops;
        }
        return row.Usage == UsageTraffic || row.Id.StartsWith("cs_", StringComparison.Ordinal) ? VehicleGroup.Traffic : VehicleGroup.Other;
    }

    private static VehicleSafety SafetyOf(CatalogRow row, CarInfo? info)
    {
        if (IsUnsafe(row.Id))
        {
            return VehicleSafety.MayCrash;
        }
        return info is not null || ProvenVariants.Contains(row.Id) ? VehicleSafety.Drivable : VehicleSafety.AiVehicle;
    }

    private static bool IsUnsafe(string id) => id == "copheli" || id.StartsWith("trailer", StringComparison.Ordinal);

    private static (byte[], List<CatalogFamily>) Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CarCatalog.bin")!;
        using var reader = new BinaryReader(stream);
        var slots = reader.ReadBytes(Layout.PartSlots);
        var count = reader.ReadByte();
        var families = new List<CatalogFamily>(count);
        for (var f = 0; f < count; f++)
        {
            var label = Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadByte()));
            var partIds = reader.ReadBytes(reader.ReadInt32());
            var rowCount = reader.ReadUInt16();
            var rows = new List<CatalogRow>(rowCount);
            for (var r = 0; r < rowCount; r++)
            {
                var idLength = reader.ReadByte();
                var usage = reader.ReadByte();
                var id = Encoding.ASCII.GetString(reader.ReadBytes(idLength));
                var parts = new ushort[Layout.PartSlots];
                for (var slot = 0; slot < parts.Length; slot++)
                {
                    parts[slot] = reader.ReadUInt16();
                }
                rows.Add(new CatalogRow(id, usage, parts));
            }
            families.Add(new CatalogFamily(label, partIds, rows));
        }
        return (slots, families);
    }
}
