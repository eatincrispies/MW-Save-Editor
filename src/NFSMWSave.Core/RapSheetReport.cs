using System.Globalization;

namespace NFSMWSave.Core;

public sealed record RapSheetVehicle(string Name, uint Bounty, long FinesDue, int Unserved, int Evaded, int Busted, string Status);

public sealed record RapSheetInfraction(string Name, int Unserved, int Served, int Fine);

public sealed record RapSheetCost(string Name, int Times, long Value, StatField TimesField, StatField? ValueField);

public sealed record RapSheetCostLine(string Name, StatField TimesField, StatField? ValueField, int Price);

public sealed record RapSheetPursuit(string Id, string Car, int Bounty, string Length, int Cops, int Damaged, int Destroyed, int Roadblocks, int Spikes, int CostToState, int Infractions, int Helicopters);

public sealed record RapSheetRank(string Name, string Lifetime, string LifetimeRank, string Single, string SingleRank);

public sealed class RapSheetReport
{
    private const int TopPursuits = 0xA2C;
    private const int PursuitSize = 0x38;
    private const int PursuitCount = 5;
    private const int PursuitNameLength = 12;
    private const int BestPursuits = 0xB64;
    private const int BestPursuitSize = 8;
    private const int RecentCar = 0xBD4;
    private const int RankCount = 15;
    private const int LengthType = 0;
    private const int InfractionType = 8;
    private const int BountyType = 9;

    public static readonly IReadOnlyList<string> InfractionNames =
    [
        "Speeding",
        "Excessive Speeding",
        "Reckless Driving",
        "Ramming a Police Vehicle",
        "Hit and Run",
        "Damage to Property",
        "Resisting Arrest",
        "Driving off Roadway",
    ];

    private static readonly int[] Fines = [150, 350, 1000, 350, 300, 100, 300, 75];

    private static readonly int[][] SingleRanks =
    [
        [1800, 1680, 1560, 1200, 900, 600, 540, 480, 420, 360, 300, 240, 180, 120, 60],
        [150, 125, 100, 75, 50, 30, 25, 20, 15, 12, 10, 8, 6, 4, 2],
        [50, 45, 40, 35, 30, 25, 20, 15, 12, 10, 8, 6, 4, 2, 1],
        [50, 45, 40, 35, 30, 25, 20, 18, 15, 12, 10, 8, 6, 4, 2],
        [30, 25, 20, 18, 16, 14, 12, 10, 8, 6, 5, 4, 3, 2, 1],
        [30, 26, 24, 22, 20, 18, 16, 14, 12, 10, 8, 6, 4, 2, 1],
        [5, 4, 4, 4, 4, 3, 3, 3, 3, 2, 2, 2, 1, 1, 1],
        [500000, 450000, 400000, 350000, 300000, 250000, 200000, 150000, 125000, 100000, 75000, 50000, 25000, 15000, 10000],
        [7, 7, 7, 7, 6, 6, 6, 5, 5, 5, 4, 4, 3, 3, 2],
        [1500000, 1250000, 1000000, 750000, 500000, 300000, 200000, 150000, 100000, 75000, 50000, 25000, 10000, 5000, 1000],
    ];

    private static readonly (int Type, string Name)[] RankOrder =
    [
        (0, "Pursuit Length"),
        (1, "Cops Involved"),
        (2, "Cops Damaged"),
        (3, "Cops Immobilized"),
        (5, "Roadblocks Dodged"),
        (4, "Spike Strips Dodged"),
        (7, "Cost to State"),
        (8, "Infractions"),
        (6, "Helicopters Involved"),
        (9, "Bounty"),
    ];

    private readonly SaveFile _save;
    private readonly List<CareerCar> _cars;
    private readonly Dictionary<uint, string> _names = [];

    public RapSheetReport(SaveFile save)
    {
        _save = save;
        _cars = save.Stable.Cars.Where(c => c.IsCareer).ToList();
        foreach (var car in save.Stable.Cars.Concat(save.Stable.MyCars))
        {
            _names.TryAdd(car.FeKey, car.Name);
        }
    }

    public string Name => _save.Career.ProfileName;
    public string CaseFile => _save.Career.CaseFile;
    public long Bounty => _save.Stable.TotalBounty;
    public int CostToState => _save.Stats.CareerValue(7);
    public int Impounded => _cars.Count(IsImpounded);
    public long FinesDue => _cars.Sum(c => FineValue(c.UnservedCounts));
    public long FinesPaid => _cars.Sum(c => FineValue(c.ServedCounts));
    public long Evaded => _save.Stable.TotalEvaded;
    public long Busted => _save.Stable.TotalBusted;
    public int Monitored => _cars.Count;
    public long Unserved => Infractions.Sum(i => (long)i.Unserved);
    public long Served => Infractions.Sum(i => (long)i.Served);
    public int CopsDamaged => _save.Stats.CareerValue(2);
    public int CopsImmobilized => _save.Stats.CareerValue(3);

    public string PreferredVehicle
    {
        get
        {
            var best = _cars.Where(c => c.Evaded + c.Busted > 0).OrderByDescending(c => c.Evaded + c.Busted).FirstOrDefault();
            return best?.Name ?? "No record";
        }
    }

    public string RecentVehicle
    {
        get
        {
            var key = _save.Stats.ScoreKey(RecentCar);
            return key == 0 ? "No record" : CarName(key);
        }
    }

    public IReadOnlyList<RapSheetVehicle> Vehicles => _cars
        .Select(c => new RapSheetVehicle(c.Name, c.Bounty, FineValue(c.UnservedCounts), c.UnservedCounts.Sum(), c.Evaded, c.Busted, IsImpounded(c) ? "Vehicle impounded" : c.Busted > 0 ? "Confirmed to drive" : "Suspected to drive"))
        .ToList();

    public IReadOnlyList<RapSheetInfraction> Infractions
    {
        get
        {
            var history = _save.Stable.ReadHistory();
            var rows = new List<RapSheetInfraction>();
            for (var i = 0; i < InfractionNames.Count; i++)
            {
                var type = i;
                var unserved = _cars.Sum(c => c.UnservedCounts[type]) + (type < history.Unserved.Length ? history.Unserved[type] : 0);
                var served = _cars.Sum(c => c.ServedCounts[type]) + (type < history.Served.Length ? history.Served[type] : 0);
                rows.Add(new RapSheetInfraction(InfractionNames[i], unserved, served, Fines[i]));
            }
            return rows;
        }
    }

    public static readonly IReadOnlyList<RapSheetCostLine> CostLines =
    [
        new("Damage to Property", RapSheet.DamageFields[7], RapSheet.DamageFields[6], 0),
        new("Insurance Claims", RapSheet.DamageFields[1], null, 500),
        new("Patrol Vehicles Deployed", RapSheet.DamageFields[4], null, 250),
        new("Special Units Deployed", RapSheet.DamageFields[5], null, 450),
        new("Damaged Police Vehicles", RapSheet.PursuitFields[2], null, 250),
        new("Immobilized Police Vehicles", RapSheet.PursuitFields[3], null, 5000),
        new("Roadblocks Deployed", RapSheet.DamageFields[0], null, 500),
        new("Spike Strips Deployed", RapSheet.DamageFields[2], null, 250),
        new("Helicopters Deployed", RapSheet.PursuitFields[6], null, 2000),
        new("Helicopter Spike Strips", RapSheet.DamageFields[3], null, 225),
    ];

    public IReadOnlyList<RapSheetCost> Costs => CostLines
        .Select(l =>
        {
            var times = _save.Stats.Get(l.TimesField);
            var value = l.ValueField is { } valueField ? _save.Stats.Get(valueField) : (long)times * l.Price;
            return new RapSheetCost(l.Name, times, value, l.TimesField, l.ValueField);
        })
        .ToList();

    public IReadOnlyList<RapSheetPursuit> Pursuits
    {
        get
        {
            var stats = _save.Stats;
            var list = new List<RapSheetPursuit>();
            for (var i = 0; i < PursuitCount; i++)
            {
                var at = TopPursuits + i * PursuitSize;
                var length = stats.Score(at + 0x14);
                if (length == 0)
                {
                    continue;
                }
                list.Add(new RapSheetPursuit(
                    stats.ScoreText(at, PursuitNameLength),
                    CarName(stats.ScoreKey(at + 0xC)),
                    stats.Score(at + 0x10),
                    Time(length),
                    stats.Score(at + 0x18),
                    stats.Score(at + 0x1C),
                    stats.Score(at + 0x20),
                    stats.Score(at + 0x24),
                    stats.Score(at + 0x28),
                    stats.Score(at + 0x2C),
                    stats.Score(at + 0x30),
                    stats.Score(at + 0x34)));
            }
            return list;
        }
    }

    public IReadOnlyList<RapSheetRank> Rankings => RankOrder
        .Select(r =>
        {
            long lifetime = r.Type switch
            {
                InfractionType => _save.Stable.TotalInfractions,
                BountyType => _save.Stable.TotalBounty,
                _ => _save.Stats.CareerValue(r.Type),
            };
            long single = _save.Stats.Score(BestPursuits + r.Type * BestPursuitSize + 4);
            return new RapSheetRank(r.Name, Value(r.Type, lifetime), Rank(lifetime, CompletionStats.RapSheetRanks[r.Type], r.Type), Value(r.Type, single), Rank(single, SingleRanks[r.Type], r.Type));
        })
        .ToList();

    public static string Time(long ticks)
    {
        var span = TimeSpan.FromSeconds(ticks / (double)RapSheet.TicksPerSecond);
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span:mm\\:ss}" : span.ToString("m\\:ss", CultureInfo.InvariantCulture);
    }

    private static string Value(int type, long value) => type == LengthType ? Time(value) : value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Rank(long value, int[] table, int type)
    {
        var scale = type == LengthType ? RapSheet.TicksPerSecond : 1;
        for (var i = 0; i < RankCount; i++)
        {
            if (value >= (long)table[i] * scale)
            {
                return $"#{i + 1}";
            }
        }
        return "Unranked";
    }

    private static bool IsImpounded(CareerCar car) => car.Impound is ImpoundState.BustedByCross or ImpoundState.StrikeLimit or ImpoundState.NoFunds;

    private static long FineValue(int[] counts)
    {
        long total = 0;
        for (var i = 0; i < counts.Length && i < Fines.Length; i++)
        {
            total += (long)counts[i] * Fines[i];
        }
        return total;
    }

    private string CarName(uint feKey) => _names.TryGetValue(feKey, out var name) ? name : CarCatalog.FrontendName(feKey) ?? CarCatalog.VehicleName(feKey) ?? $"Unknown car {feKey:X8}";
}
