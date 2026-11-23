using System.Reflection;

namespace NFSMWSave.Core;

[Flags]
public enum RaceFlags : uint
{
    QuickRaceCompleted = 1,
    CareerCompleted = 2,
    QuickRaceUnlocked = 4,
    CareerUnlocked = 8,
    OnlineUnlocked = 16,
}

public sealed class RaceRecord
{
    private readonly SaveBuffer _d;
    private readonly int _at;

    internal RaceRecord(SaveBuffer d, int at, string id)
    {
        _d = d;
        _at = at;
        Id = id;
        var dot = id.IndexOf('.');
        Group = dot > 0 && int.TryParse(id[..dot], out var group) ? group : 0;
    }

    public string Id { get; }
    public int Group { get; }
    public bool IsBlacklist => Group is >= Career.FirstRank and <= Career.LastRank;

    public RaceFlags Flags
    {
        get => (RaceFlags)_d.U32(_at + 4);
        set => _d.U32(_at + 4, (uint)value);
    }

    public bool Has(RaceFlags flag) => (Flags & flag) != 0;

    public void Set(RaceFlags flag, bool on) => Flags = on ? Flags | flag : Flags & ~flag;
}

public sealed class RaceTable
{
    private static readonly Dictionary<uint, string> Names = LoadNames();

    public RaceTable(SaveBuffer d)
    {
        var count = Math.Min(d.U32(d.Map.RaceCount), (uint)Layout.MaxRaces);
        var races = new List<RaceRecord>();
        for (var i = 0; i < count; i++)
        {
            var at = d.Map.Races + i * Layout.RaceSize;
            if (Names.TryGetValue(d.U32(at), out var id))
            {
                races.Add(new RaceRecord(d, at, id));
            }
        }
        Races = races.OrderBy(r => SortKey(r.Id)).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<RaceRecord> Races { get; }

    public void UnlockAllQuickRaces()
    {
        foreach (var race in Races)
        {
            race.Set(RaceFlags.QuickRaceUnlocked, true);
        }
    }

    private static long SortKey(string id)
    {
        long key = 0;
        var parts = id.Replace(".r", "").Split('.');
        for (var i = 0; i < 3; i++)
        {
            key = key * 1000 + (i < parts.Length && int.TryParse(parts[i], out var v) ? v : 999);
        }
        return key;
    }

    private static Dictionary<uint, string> LoadNames()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Races.txt")!;
        using var reader = new StreamReader(stream);
        var names = new Dictionary<uint, string>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                names[AttribHash.Of(line)] = line;
            }
        }
        return names;
    }
}
