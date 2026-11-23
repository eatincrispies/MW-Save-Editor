using System.Globalization;
using System.IO.Compression;
using System.Reflection;

namespace NFSMWSave.Core;

internal sealed record PresetRow(string Name, uint Hash, uint VehicleKey, uint FeKey, int[] Levels, ushort[] Parts);

internal static class CarPresets
{
    private static readonly Lazy<Dictionary<string, List<PresetRow>>> Data = new(Load);

    private static readonly Dictionary<string, int> BossRanks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["E3_DEMO_BMW"] = 1,
        ["BL3"] = 2,
        ["BL2"] = 3,
        ["BL6"] = 4,
        ["BL5"] = 5,
        ["BL4"] = 6,
        ["BL7"] = 7,
        ["BL8"] = 8,
        ["BL9"] = 9,
        ["BL10"] = 10,
        ["BL11"] = 11,
        ["BL12"] = 12,
        ["BL13"] = 13,
        ["BL14"] = 14,
        ["BL15"] = 15,
    };

    private static readonly Dictionary<string, string> Titles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CastrolGT"] = "Castrol Ford GT",
        ["E3_DEMO_RX8"] = "E3 Demo Mazda RX-8",
        ["M3GTRCAREERSTART"] = CarCatalog.PrologueName,
        ["RAZORMUSTANG"] = "Razor's Ford Mustang GT",
        ["OPM_MUSTANG_BOSS"] = "Rog's Pontiac GTO",
        ["DDAYSUPRA"] = "Ronnie's Toyota Supra",
        ["COP_CROSS"] = "Sgt. Cross's Corvette",
        ["COPGTOPURSUIT"] = "Pursuit Police GTO",
    };

    public static IReadOnlyList<PresetRow> For(string family) => Data.Value.TryGetValue(family, out var rows) ? rows : [];

    public static int BossRank(string name) => BossRanks.GetValueOrDefault(name);

    public static string Title(string name, string carName)
    {
        if (Titles.TryGetValue(name, out var title))
        {
            return title;
        }
        var rank = BossRank(name);
        if (rank > 0)
        {
            var rival = GameData.Rivals.First(r => r.Rank == rank);
            return $"#{rank} {rival.Name} ({rival.Car})";
        }
        if (name.StartsWith("CE_", StringComparison.OrdinalIgnoreCase))
        {
            return $"Black Edition {carName}";
        }
        if (name.StartsWith("BONUS_", StringComparison.OrdinalIgnoreCase))
        {
            return $"Bonus {carName}";
        }
        if (name.StartsWith("CS_CAR_", StringComparison.OrdinalIgnoreCase))
        {
            return $"Challenge {Pretty(name[7..])} ({carName})";
        }
        return $"{Pretty(name)} ({carName})";
    }

    private static string Pretty(string text) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.Replace('_', ' ').ToLowerInvariant());

    private static uint UpperHash(string text)
    {
        var hash = 0xFFFFFFFFu;
        foreach (var c in text.ToUpperInvariant())
        {
            hash = hash * 33 + (byte)c;
        }
        return hash;
    }

    private static Dictionary<string, List<PresetRow>> Load()
    {
        var families = new Dictionary<string, List<PresetRow>>();
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CarPresets.gz")!;
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        List<PresetRow>? current = null;
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split('\t');
            if (fields[0] == "F")
            {
                current = [];
                families[fields[1]] = current;
            }
            else if (fields[0] == "P" && current is not null)
            {
                current.Add(new PresetRow(
                    fields[1],
                    UpperHash(fields[1]),
                    uint.Parse(fields[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    uint.Parse(fields[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    fields[4].Split(',').Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                    fields[5].Split(',').Select(v => ushort.Parse(v, CultureInfo.InvariantCulture)).ToArray()));
            }
        }
        return families;
    }
}
