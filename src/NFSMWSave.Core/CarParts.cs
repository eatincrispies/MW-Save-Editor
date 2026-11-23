using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace NFSMWSave.Core;

public sealed record PartChoice(ushort Part, string Group, string Name);

public sealed record PartKind(string Title, int Slot);

public sealed record DecalPlace(string Title, int FirstSlot, int SlotCount);

public sealed record CarVisualChoices(
    IReadOnlyList<PartChoice> Tints,
    IReadOnlyList<PartChoice> Gauges,
    IReadOnlyList<PaintSwatch> GaugeColours,
    IReadOnlyList<PartChoice> Decals,
    IReadOnlyList<PartChoice> LeftNumbers,
    IReadOnlyList<PartChoice> RightNumbers);

public static class CarParts
{
    public const int BodySlot = 23;
    public const int SpoilerSlot = 44;
    public const int RoofSlot = 62;
    public const int HoodSlot = 63;
    public const int RimSlot = 66;
    public const int RearRimSlot = 67;
    public const int WindowTintSlot = 131;
    public const int GaugeSlot = 132;
    public const int GaugeFaceSlot = 133;
    public const int GaugeNeedleSlot = 134;
    public const int GaugeLetteringSlot = 135;
    public const int LeftDoorLeftNumber = 105;
    public const int LeftDoorRightNumber = 106;
    public const int RightDoorLeftNumber = 113;
    public const int RightDoorRightNumber = 114;
    public const string StockGroup = "Stock";

    public static readonly IReadOnlyList<PartKind> Kinds =
    [
        new("Body Kits", BodySlot),
        new("Spoilers", SpoilerSlot),
        new("Rims", RimSlot),
        new("Hoods", HoodSlot),
        new("Roof Scoops", RoofSlot),
    ];

    public static readonly IReadOnlyList<string> SpoilerTypes = [StockGroup, "Sport", "Tuner", "Sport Carbon", "Tuner Carbon"];

    public static readonly IReadOnlyList<string> ScoopTypes = [StockGroup, "Standard", "Carbon"];

    public static readonly IReadOnlyList<DecalPlace> DecalPlaces =
    [
        new("Windshield", 83, 1),
        new("Rear Window", 91, 1),
        new("Left Door", 99, 6),
        new("Right Door", 107, 6),
        new("Left Quarter Panel", 115, 1),
        new("Right Quarter Panel", 123, 1),
    ];

    internal static readonly IReadOnlyList<int> VisualSlots = DecalPlaces
        .SelectMany(p => Enumerable.Range(p.FirstSlot, p.SlotCount))
        .Concat([WindowTintSlot, GaugeSlot, GaugeFaceSlot, GaugeNeedleSlot, GaugeLetteringSlot, LeftDoorLeftNumber, LeftDoorRightNumber, RightDoorLeftNumber, RightDoorRightNumber])
        .ToList();

    private static readonly Lazy<Dictionary<string, FamilyParts>> Data = new(Load);

    internal static IReadOnlyList<PartChoice> For(CatalogFamily family, uint vehicleKey, int slot) =>
        Data.Value.TryGetValue(family.Label, out var parts) && parts.Lists.TryGetValue((CarCatalog.PartsKey(vehicleKey), slot), out var list) ? list : [];

    internal static uint? NameHash(CatalogFamily family, ushort part) =>
        Data.Value.TryGetValue(family.Label, out var parts) && parts.Hashes.TryGetValue(part, out var hash) ? hash : null;

    internal static ushort? MatchPart(CatalogFamily family, int slot, ushort part, uint? hash)
    {
        var fits = CarCatalog.PartFits(family, part, slot);
        if (hash is null)
        {
            return fits ? part : null;
        }
        if (fits && (CarPaints.NameHash(part) ?? NameHash(family, part)) == hash)
        {
            return part;
        }
        if (CarPaints.FindByHash(hash.Value, candidate => CarCatalog.PartFits(family, candidate, slot)) is { } paint)
        {
            return paint;
        }
        if (Data.Value.TryGetValue(family.Label, out var parts))
        {
            foreach (var (candidate, value) in parts.Hashes)
            {
                if (value == hash && CarCatalog.PartFits(family, candidate, slot))
                {
                    return candidate;
                }
            }
        }
        return null;
    }

    internal static int[]? UpgradeLimits(CatalogFamily family, uint vehicleKey) =>
        Data.Value.TryGetValue(family.Label, out var parts) && parts.Limits.TryGetValue(vehicleKey, out var limits) ? limits : null;

    internal static CarVisualChoices? Visuals(CatalogFamily family)
    {
        if (!Data.Value.TryGetValue(family.Label, out var parts) || !parts.Visuals.ContainsKey("Tint"))
        {
            return null;
        }
        return new CarVisualChoices(parts.Visuals["Tint"], parts.Visuals["Gauge"], parts.GaugeColours, parts.Visuals["Decal"], parts.Visuals["NumberLeft"], parts.Visuals["NumberRight"]);
    }

    private static Dictionary<string, FamilyParts> Load()
    {
        var families = new Dictionary<string, FamilyParts>();
        FamilyParts? current = null;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CarParts.gz")!;
        using var unzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(unzip, Encoding.UTF8);
        while (reader.ReadLine() is { } line)
        {
            var f = line.Split('\t');
            switch (f[0])
            {
                case "F":
                    current = new FamilyParts();
                    families[f[1]] = current;
                    break;
                case "N":
                    var part = ushort.Parse(f[1]);
                    current!.Hashes[part] = uint.Parse(f[2], NumberStyles.HexNumber);
                    current.Labels[part] = (f[4], f[5]);
                    break;
                case "H":
                    var colour = ushort.Parse(f[1]);
                    current!.GaugeColours.Add(new PaintSwatch(colour, "Gauge", current.Labels[colour].Name, byte.Parse(f[2]), byte.Parse(f[3]), byte.Parse(f[4])));
                    break;
                case "V":
                    current!.Visuals[f[1]] = f[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(ushort.Parse)
                        .Select(n => new PartChoice(n, current.Labels[n].Group, current.Labels[n].Name))
                        .ToList();
                    break;
                case "M":
                    current!.Limits[uint.Parse(f[1], NumberStyles.HexNumber)] = f[2].Split(',').Select(int.Parse).ToArray();
                    break;
                case "C":
                    var slot = int.Parse(f[2]);
                    current!.Lists[(AttribHash.Of(f[1]), slot)] = BuildList(current, slot, f[3]);
                    break;
            }
        }
        return families;
    }

    private static List<PartChoice> BuildList(FamilyParts family, int slot, string numbers)
    {
        var list = new List<PartChoice>();
        var seen = new Dictionary<(string, string), int>();
        foreach (var number in numbers.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = ushort.Parse(number);
            var (group, name) = family.Labels[part];
            if (list.Count == 0 && slot != BodySlot)
            {
                group = StockGroup;
                if (slot == RimSlot)
                {
                    name = StockGroup;
                }
            }
            var count = seen[(group, name)] = seen.GetValueOrDefault((group, name)) + 1;
            list.Add(new PartChoice(part, group, count > 1 ? $"{name} {count}" : name));
        }
        return list;
    }

    private sealed class FamilyParts
    {
        public Dictionary<ushort, uint> Hashes { get; } = [];
        public Dictionary<ushort, (string Group, string Name)> Labels { get; } = [];
        public Dictionary<(uint Vehicle, int Slot), List<PartChoice>> Lists { get; } = [];
        public Dictionary<string, List<PartChoice>> Visuals { get; } = [];
        public List<PaintSwatch> GaugeColours { get; } = [];
        public Dictionary<uint, int[]> Limits { get; } = [];
    }
}
