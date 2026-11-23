using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NFSMWSave.Core;

public sealed record PaintSwatch(ushort Part, string Finish, string Name, byte R, byte G, byte B)
{
    public override string ToString() => Name;
}

public sealed record VinylChoice(ushort Part, int Group, string Name, int Colours);

public static class CarPaints
{
    public const int PaintSlot = 76;
    public const int VinylSlot = 77;
    public const int RimPaintSlot = 78;
    public const int VinylColourSlot = 79;
    public const int VinylColourSlots = 4;
    public const int MaxVinylColours = 2;
    public const ushort NoPart = 0xFFFF;

    public static readonly string[] Finishes = ["Gloss", "Metallic", "Pearl"];
    public static readonly string[] VinylGroups = ["Flames", "Tribal", "Stripes", "Racing Flags", "National Flags", "Body", "Unique", "Contest", "Hidden"];

    private const int HiddenGroup = 8;

    private static readonly int[] StockVinylColours = [1, 3, 0, 1];

    private static readonly Dictionary<string, string> HiddenNames = new()
    {
        ["PSRTEST"] = "Hero",
        ["BMW_24"] = "BMW #24",
        ["CROSS"] = "Cross",
        ["CROSS_HENCHMEN"] = "Cross Henchmen",
        ["COPGTO"] = "Cop GTO",
        ["CE01"] = "Black Edition 1",
        ["CE02"] = "Black Edition 2",
        ["CE03"] = "Black Edition 3",
    };

    private static readonly Dictionary<string, int> RivalVinyls = new()
    {
        ["01RAZORCALLAHAN"] = 1,
        ["03RONNIEMCCREA"] = 2,
        ["02TUROSATO"] = 3,
        ["06HECTORDOMINGO"] = 4,
        ["05WESALLEN"] = 5,
        ["04JOEVEGA"] = 6,
        ["07KIRANAZAKATO"] = 7,
        ["08JADEBARRETT"] = 8,
        ["09PIERREDUPONT"] = 9,
        ["10KARLSMIT"] = 10,
        ["11LOUPARK"] = 11,
        ["12ISABELDIAZ"] = 12,
        ["13VICTORVASQUEZ"] = 13,
        ["14VINCEKILIC"] = 14,
        ["15TONYHO"] = 15,
    };

    private static readonly Lazy<PaintData> Data = new(Load);

    public static IReadOnlyList<PaintSwatch> Paints => Data.Value.Paints;

    public static IReadOnlyList<PaintSwatch> VinylColours => Data.Value.Colours;

    public static IReadOnlyList<PaintSwatch> RimPaints => Data.Value.RimPaints;

    public static IReadOnlyList<VinylChoice> VinylsFor(uint vehicleKey) => Data.Value.ByVehicle.GetValueOrDefault(CarCatalog.PartsKey(vehicleKey)) ?? [];

    public static VinylChoice? FindVinyl(ushort part) => Data.Value.Vinyls.GetValueOrDefault(part);

    internal static ushort[] StockColourParts => Data.Value.StockColours;

    internal static uint? NameHash(ushort part) => Data.Value.Hashes.TryGetValue(part, out var hash) ? hash : null;

    internal static ushort? FindByHash(uint hash, Func<ushort, bool> fits)
    {
        foreach (var (part, value) in Data.Value.Hashes)
        {
            if (value == hash && fits(part))
            {
                return part;
            }
        }
        return null;
    }

    internal static bool FitsFamily(CatalogFamily family)
    {
        var data = Data.Value;
        return data.Paints.All(p => CarCatalog.PartFits(family, p.Part, PaintSlot))
            && data.Colours.All(c => CarCatalog.PartFits(family, c.Part, VinylColourSlot))
            && data.RimPaints.All(r => CarCatalog.PartFits(family, r.Part, RimPaintSlot))
            && data.Vinyls.Keys.All(v => CarCatalog.PartFits(family, v, VinylSlot));
    }

    private static PaintData Load()
    {
        var paints = new List<PaintSwatch>();
        var colours = new List<PaintSwatch>();
        var rimPaints = new List<PaintSwatch>();
        var vinyls = new Dictionary<ushort, VinylChoice>();
        var byVehicle = new Dictionary<uint, IReadOnlyList<VinylChoice>>();
        var hashes = new Dictionary<ushort, uint>();
        var stock = new Dictionary<int, ushort>();
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CarPaints.txt")!;
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            var f = line.Split('\t');
            switch (f[0])
            {
                case "P":
                    var paint = new PaintSwatch(ushort.Parse(f[1]), f[2], f[3], byte.Parse(f[4]), byte.Parse(f[5]), byte.Parse(f[6]));
                    paints.Add(paint);
                    hashes[paint.Part] = uint.Parse(f[7], NumberStyles.HexNumber);
                    break;
                case "C":
                    var part = ushort.Parse(f[1]);
                    var number = int.Parse(f[2]);
                    stock[number] = part;
                    hashes[part] = uint.Parse(f[6], NumberStyles.HexNumber);
                    colours.Add(new PaintSwatch(part, "Vinyl", $"Colour {number:00}", byte.Parse(f[3]), byte.Parse(f[4]), byte.Parse(f[5])));
                    break;
                case "R":
                    var rimPaint = new PaintSwatch(ushort.Parse(f[1]), f[2], f[3], byte.Parse(f[4]), byte.Parse(f[5]), byte.Parse(f[6]));
                    rimPaints.Add(rimPaint);
                    hashes[rimPaint.Part] = uint.Parse(f[7], NumberStyles.HexNumber);
                    break;
                case "V":
                    var group = int.Parse(f[2]);
                    var vinyl = new VinylChoice(ushort.Parse(f[1]), group, group == HiddenGroup ? HiddenName(f[3]) : Pretty(f[3]), Math.Min(int.Parse(f[4]), MaxVinylColours));
                    vinyls[vinyl.Part] = vinyl;
                    hashes[vinyl.Part] = uint.Parse(f[5], NumberStyles.HexNumber);
                    break;
                case "A":
                    byVehicle[AttribHash.Of(f[1])] = f[2].Split(',').Select(n => vinyls[ushort.Parse(n)]).ToList();
                    break;
            }
        }
        return new PaintData(paints, colours, rimPaints, vinyls, byVehicle, hashes, StockVinylColours.Select(c => c == 0 ? NoPart : stock[c]).ToArray());
    }

    private static string HiddenName(string texture)
    {
        if (HiddenNames.TryGetValue(texture, out var name))
        {
            return name;
        }
        if (RivalVinyls.TryGetValue(texture, out var rank))
        {
            var rival = GameData.Rivals.First(r => r.Rank == rank);
            return $"{rival.Name} (#{rival.Rank})";
        }
        return Pretty(texture);
    }

    private static string Pretty(string texture)
    {
        var words = Regex.Matches(texture, "[0-9]+|[A-Za-z]+").Select(m => m.Value);
        return string.Join(' ', words.Select(w => w.Length > 3 && char.IsLetter(w[0]) ? char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant() : w));
    }

    private sealed record PaintData(
        List<PaintSwatch> Paints,
        List<PaintSwatch> Colours,
        List<PaintSwatch> RimPaints,
        Dictionary<ushort, VinylChoice> Vinyls,
        Dictionary<uint, IReadOnlyList<VinylChoice>> ByVehicle,
        Dictionary<ushort, uint> Hashes,
        ushort[] StockColours);
}
