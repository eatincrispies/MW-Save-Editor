using System.Globalization;

namespace NFSMWSave.Core;

public sealed record BinStatus(int Rank, int Challenges, int RaceWins, int MilestonesDone, int MilestonesTotal);

public sealed class ChallengeRecord(int rank, int id, bool isTrap, string name, string goal, Func<bool> isDone)
{
    public int Rank { get; } = rank;
    public int Id { get; } = id;
    public bool IsTrap { get; } = isTrap;
    public string Name { get; } = name;
    public string Goal { get; } = goal;
    public bool IsDone => isDone();
}

public sealed class CareerProgress
{
    private const int HeaderFields = 0x10;
    private const int ObjectCountField = 0x18;
    private const int TimerCountField = 0x1C;
    private const int MilestoneTypeCountField = 0x20;
    private const int MilestoneCountField = 0x24;
    private const int TrapCountField = 0x28;
    private const int BitCountField = 0x30;
    private const int FirstBlock = 0x80;
    private const int TimerSize = 32;
    private const int MilestoneTypeSize = 16;
    private const int MilestoneSize = 20;
    private const int MilestoneState = 8;
    private const int MilestoneBin = 10;
    private const int MilestoneRequired = 12;
    private const int MilestoneRecorded = 16;
    private const byte StateLocked = 1;
    private const byte StateAvailable = 2;
    private const byte StateAwarded = 4;
    private const ushort TrapUnlocked = 1;
    private const ushort TrapActive = 2;
    private const ushort TrapCompleted = 4;
    private const int MaxBins = 32;
    private const string FinalBossRace = "1.2.3";
    private const string FinalPursuitRace = "1.8.1";
    private const string PrologueEndRace = "16.2.1";
    private const uint TypeTradePaint = 0x850A64BC;
    private const uint TypeCostToState = 0xFD989A3A;
    private const uint TypePursuitEvasion = 0x5392E4FD;
    private const uint TypePursuitTime = 0x033FA23A;
    private const uint TypeRoadblocks = 0xEB45F99D;
    private const uint TypeSpikeStrips = 0xCDF36FC2;
    private const uint TypeInfractions = 0xA61CAC24;
    private const uint TypeBounty = 0x2377E50D;
    private const float MpsToMph = 2.23694f;
    private const float MpsToKph = 3.6f;

    private static readonly string[][] WorldRaces =
    [
        ["1.1.1", "1.2.1", "1.2.2", "1.2.4", "1.3.1", "1.4.1", "1.4.2", "1.5.1", "1.5.3", "2.2.1.r", "2.4.3.r"],
        ["2.1.1", "2.1.1.r", "2.4.1", "2.4.2", "2.4.3", "2.5.1", "3.2.1.r", "3.3.1.r", "3.3.2.r", "5.1.1.r", "5.5.1.r", "2.7.1"],
        ["3.2.1", "3.3.1", "3.1.1", "3.4.1", "7.2.1.r", "4.3.1.r", "3.5.1", "3.5.2", "3.4.1.r", "4.4.1.r", "4.2.1.r", "3.7.3"],
        ["4.4.1", "4.1.1", "4.3.1", "4.5.3", "4.5.1", "5.5.2.r", "7.2.2.r", "6.3.1.r", "9.1.1.r", "5.4.1.r", "7.4.3.r"],
        ["5.3.1", "5.1.1", "5.2.1", "5.5.1", "5.5.2", "5.4.1", "5.5.3", "5.2.3", "6.4.1.r", "7.4.2.r"],
        ["6.2.2", "6.4.1", "6.5.1", "6.2.1", "6.4.2", "6.3.1", "14.3.1.r", "6.2.3", "6.5.3", "7.7.1.r", "7.4.1.r"],
        ["7.2.1", "7.4.3", "7.5.1", "7.4.1", "7.3.1", "16.1.1.r", "8.2.1.r", "7.1.1", "7.4.2", "7.7.4"],
        ["8.3.1", "8.3.2", "8.5.1", "8.5.2", "8.2.2", "10.4.2.r", "9.4.1.r"],
        ["9.1.1", "9.7.1", "9.4.1", "9.5.1", "9.5.2", "9.4.2", "12.1.1.r", "9.3.1"],
        ["10.3.1", "10.3.2", "10.4.2", "10.4.1", "11.2.1.r", "10.2.2", "11.1.2.r", "11.5.1.r"],
        ["11.2.1", "11.1.1", "11.5.1", "11.4.2", "11.4.1", "12.2.1.r", "11.5.2", "11.7.1"],
        ["12.1.1", "12.4.1", "12.4.2", "12.5.1", "12.7.3", "13.2.2", "14.2.3"],
        ["13.2.1", "13.3.1", "13.3.2", "13.4.1", "13.4.2", "13.5.1", "13.5.2"],
        ["14.1.1", "14.2.1", "14.2.2", "14.3.1", "14.4.1", "14.1.2"],
        ["15.1.1", "15.2.1", "15.3.1"],
    ];

    private readonly SaveBuffer _d;
    private readonly Career _career;
    private readonly RaceTable _races;
    private readonly int _milestones;
    private readonly int _milestoneCount;
    private readonly int _traps;
    private readonly int _trapCount;
    private readonly Dictionary<int, int> _binRows = [];

    public CareerProgress(SaveBuffer d, Career career, RaceTable races)
    {
        _d = d;
        _career = career;
        _races = races;
        var g = Layout.GameplayOffset;
        if (d.U32(g + HeaderFields) != Layout.GameplayMagic || d.U32(g + HeaderFields + 4) < d.Map.MinGameplayVersion)
        {
            return;
        }
        var end = g + Layout.GameplaySize;
        var p = g + FirstBlock;
        var objects = d.U32(g + ObjectCountField);
        for (var i = 0; i < objects; i++)
        {
            if (p + 8 > end)
            {
                return;
            }
            p += (int)((d.U32(p + 4) + 0x17) & ~0xFu);
        }
        p = Align(p);
        p = Align(p + (int)d.U32(g + TimerCountField) * TimerSize);
        p = Align(p + (int)d.U32(g + MilestoneTypeCountField) * MilestoneTypeSize);
        var milestones = p;
        var milestoneCount = (int)d.U32(g + MilestoneCountField);
        p = Align(p + milestoneCount * MilestoneSize);
        var traps = p;
        var trapCount = (int)d.U32(g + TrapCountField);
        p = Align(p + trapCount * _d.Map.TrapSize);
        p = Align(p + (int)((d.U32(g + BitCountField) + 7) / 8));
        if (p + 4 > end)
        {
            return;
        }
        var bins = d.U32(p);
        if (bins > MaxBins)
        {
            return;
        }
        var q = p + 4;
        for (var i = 0; i < bins; i++)
        {
            if (q + 8 > end)
            {
                return;
            }
            _binRows[d.U16(q)] = q + 4;
            q += 4 + d.U16(q + 2);
        }
        _milestones = milestones;
        _milestoneCount = milestoneCount;
        _traps = traps;
        _trapCount = trapCount;
        IsReadable = true;
    }

    public bool IsReadable { get; }

    public int MilestoneCount => _milestoneCount;

    public int MilestonesAwarded
    {
        get
        {
            var awarded = 0;
            for (var i = 0; i < _milestoneCount; i++)
            {
                awarded += _d[_milestones + i * MilestoneSize + MilestoneState] == StateAwarded ? 1 : 0;
            }
            return awarded;
        }
    }

    public bool CanChangeRank => IsReadable && _career.HasStarted && _career.Rank is >= Career.FirstRank and <= Career.LastRank;

    public string? HeldBackName(RaceRecord race)
    {
        if (race.Has(RaceFlags.CareerCompleted))
        {
            return null;
        }
        var beaten = _career.GetFlag(CareerFlags.BeatenCareer);
        return race.Id switch
        {
            FinalBossRace when !beaten => "Final boss race",
            FinalPursuitRace when !beaten => "Final pursuit",
            PrologueEndRace when _career.Rank == Career.PrologueRank => "Last prologue race",
            _ => null,
        };
    }

    public bool IsAheadOfRank(RaceRecord race) =>
        race.IsBlacklist && race.Group < _career.Rank && !race.Has(RaceFlags.CareerCompleted) && !_career.GetFlag(CareerFlags.BeatenCareer);

    public bool CanEditMilestones(int rank) => CanChangeRank && rank >= _career.Rank && rank <= Career.LastRank && _binRows.ContainsKey(rank);

    public IReadOnlyList<ChallengeRecord> Challenges
    {
        get
        {
            var list = new List<ChallengeRecord>();
            if (!IsReadable)
            {
                return list;
            }
            for (var rank = Career.FirstRank; rank <= Career.LastRank; rank++)
            {
                foreach (var at in MilestonesIn(rank))
                {
                    var type = _d.U32(at);
                    list.Add(new ChallengeRecord(rank, at, false, MilestoneName(type), MilestoneGoal(type, _d.F32(at + MilestoneRequired)), () => _d[at + MilestoneState] == StateAwarded));
                }
                foreach (var at in TrapsIn(rank))
                {
                    list.Add(new ChallengeRecord(rank, at, true, "Photo Ticket", SpeedGoal(_d.F32(at + _d.Map.TrapRequired)), () => (_d.U16(at + _d.Map.TrapFlags) & TrapCompleted) != 0));
                }
            }
            return list;
        }
    }

    public void SetChallenges(IEnumerable<ChallengeRecord> challenges, bool complete)
    {
        var ranks = new HashSet<int>();
        foreach (var challenge in challenges.Where(c => CanEditMilestones(c.Rank)))
        {
            if (challenge.IsTrap)
            {
                SetTrap(challenge.Id, complete);
            }
            else
            {
                SetMilestone(challenge.Id, complete);
            }
            ranks.Add(challenge.Rank);
        }
        foreach (var rank in ranks)
        {
            _d.U16(_binRows[rank], (ushort)CountChallenges(rank));
        }
    }

    private void SetMilestone(int at, bool complete)
    {
        var awarded = _d[at + MilestoneState] == StateAwarded;
        if (complete && !awarded)
        {
            _d[at + MilestoneState] = StateAwarded;
            _d.F32(at + MilestoneRecorded, _d.F32(at + MilestoneRequired));
        }
        else if (!complete && awarded)
        {
            _d[at + MilestoneState] = StateAvailable;
            _d.F32(at + MilestoneRecorded, 0f);
        }
    }

    private void SetTrap(int at, bool complete)
    {
        var finished = (_d.U16(at + _d.Map.TrapFlags) & TrapCompleted) != 0;
        if (complete && !finished)
        {
            _d.U16(at + _d.Map.TrapFlags, TrapUnlocked | TrapCompleted);
            _d.F32(at + _d.Map.TrapRecorded, _d.F32(at + _d.Map.TrapRequired));
        }
        else if (!complete && finished)
        {
            _d.U16(at + _d.Map.TrapFlags, TrapUnlocked | TrapActive);
            _d.F32(at + _d.Map.TrapRecorded, 0f);
        }
    }

    private static string MilestoneName(uint type) => type switch
    {
        TypeTradePaint => "Trade Paint",
        TypeCostToState => "Cost to State Challenge",
        TypePursuitEvasion => "Pursuit Evasion",
        TypePursuitTime => "Pursuit Time Challenge",
        TypeRoadblocks => "Roadblock Challenge",
        TypeSpikeStrips => "Spike Strip Challenge",
        TypeInfractions => "Infractions Challenge",
        TypeBounty => "Bounty",
        _ => "Milestone",
    };

    private static string MilestoneGoal(uint type, float required)
    {
        var amount = required.ToString("N0", CultureInfo.InvariantCulture);
        var minutes = (required / 60f).ToString("0.#", CultureInfo.InvariantCulture);
        return type switch
        {
            TypeTradePaint => $"Tag {amount} police vehicles",
            TypeCostToState => $"${amount} cost to state",
            TypePursuitEvasion => $"Evade in under {minutes} minutes",
            TypePursuitTime => $"Last {minutes} minutes",
            TypeRoadblocks => $"Dodge {amount} roadblocks",
            TypeSpikeStrips => $"Dodge {amount} spike strips",
            TypeInfractions => $"{amount} infractions",
            TypeBounty => $"{amount} bounty in one pursuit",
            _ => amount,
        };
    }

    private static string SpeedGoal(float metersPerSecond)
    {
        var mph = (metersPerSecond * MpsToMph).ToString("0", CultureInfo.InvariantCulture);
        var kph = (metersPerSecond * MpsToKph).ToString("0", CultureInfo.InvariantCulture);
        return $"Over {mph} mph ({kph} km/h)";
    }

    public void RecountRaceWins(IEnumerable<RaceRecord> changed)
    {
        if (!IsReadable)
        {
            return;
        }
        var ids = changed.Select(r => r.Id).ToHashSet();
        var done = _races.Races.Where(r => r.Has(RaceFlags.CareerCompleted)).Select(r => r.Id).ToHashSet();
        for (var i = 0; i < WorldRaces.Length; i++)
        {
            if (WorldRaces[i].Any(ids.Contains) && _binRows.TryGetValue(i + Career.FirstRank, out var row))
            {
                _d.U16(row + 2, (ushort)WorldRaces[i].Count(done.Contains));
            }
        }
    }

    public BinStatus GetBin(int rank)
    {
        if (!IsReadable || !_binRows.TryGetValue(rank, out var row))
        {
            return new BinStatus(rank, 0, 0, 0, 0);
        }
        var done = 0;
        var total = 0;
        for (var i = 0; i < _milestoneCount; i++)
        {
            var at = _milestones + i * MilestoneSize;
            if (_d.U16(at + MilestoneBin) == rank)
            {
                total++;
                done += _d[at + MilestoneState] == StateAwarded ? 1 : 0;
            }
        }
        return new BinStatus(rank, _d.U16(row), _d.U16(row + 2), done, total);
    }

    public void SetRank(int target)
    {
        if (!CanChangeRank)
        {
            throw new InvalidOperationException("This profile has not started a career.");
        }
        target = Math.Clamp(target, Career.FirstRank, Career.LastRank);
        var current = _career.Rank;
        if (target == current)
        {
            return;
        }
        var movingBack = target > current;
        UpdateMilestones(target);
        UpdateTraps(target);
        UpdateRaces(target, movingBack);
        UpdateBinStats(target, movingBack);
        if (movingBack)
        {
            _career.Flags &= ~(CareerFlags.BeatenCareer | CareerFlags.GameOver);
        }
        _career.Rank = target;
    }

    internal (IReadOnlyList<ProgressState> Milestones, IReadOnlyList<ProgressState> Traps) BinStates(int rank)
    {
        var milestones = MilestonesIn(rank).Select(at => new ProgressState(_d[at + MilestoneState], _d.F32(at + MilestoneRecorded))).ToList();
        var traps = TrapsIn(rank).Select(at => new ProgressState(_d.U16(at + _d.Map.TrapFlags), _d.F32(at + _d.Map.TrapRecorded))).ToList();
        return (milestones, traps);
    }

    internal bool ApplyBin(ImportedBin bin)
    {
        var milestones = MilestonesIn(bin.Rank).ToList();
        var traps = TrapsIn(bin.Rank).ToList();
        if (!_binRows.TryGetValue(bin.Rank, out var row) || milestones.Count != bin.Milestones.Count || traps.Count != bin.Traps.Count)
        {
            return false;
        }
        for (var i = 0; i < milestones.Count; i++)
        {
            var state = bin.Milestones[i];
            if (state.State is StateLocked or StateAvailable or StateAwarded)
            {
                _d[milestones[i] + MilestoneState] = (byte)state.State;
                _d.F32(milestones[i] + MilestoneRecorded, state.Recorded);
            }
        }
        for (var i = 0; i < traps.Count; i++)
        {
            var state = bin.Traps[i];
            if (state.State is >= 0 and <= ushort.MaxValue)
            {
                _d.U16(traps[i] + _d.Map.TrapFlags, (ushort)state.State);
                _d.F32(traps[i] + _d.Map.TrapRecorded, state.Recorded);
            }
        }
        _d.U16(row, (ushort)Math.Clamp(bin.Challenges, 0, ushort.MaxValue));
        _d.U16(row + 2, (ushort)Math.Clamp(bin.RaceWins, 0, ushort.MaxValue));
        return true;
    }

    private IEnumerable<int> MilestonesIn(int rank) => Enumerable.Range(0, _milestoneCount)
        .Select(i => _milestones + i * MilestoneSize)
        .Where(at => _d.U16(at + MilestoneBin) == rank);

    private IEnumerable<int> TrapsIn(int rank) => Enumerable.Range(0, _trapCount)
        .Select(i => _traps + i * _d.Map.TrapSize)
        .Where(at => _d.U16(at + _d.Map.TrapBin) == rank);

    private void UpdateMilestones(int target)
    {
        for (var i = 0; i < _milestoneCount; i++)
        {
            var at = _milestones + i * MilestoneSize;
            var bin = _d.U16(at + MilestoneBin);
            if (bin is < Career.FirstRank or > Career.LastRank)
            {
                continue;
            }
            if (bin > target)
            {
                if (_d[at + MilestoneState] != StateAwarded)
                {
                    _d[at + MilestoneState] = StateAwarded;
                    _d.F32(at + MilestoneRecorded, _d.F32(at + MilestoneRequired));
                }
            }
            else
            {
                _d[at + MilestoneState] = bin == target ? StateAvailable : StateLocked;
                _d.F32(at + MilestoneRecorded, 0f);
            }
        }
    }

    private void UpdateTraps(int target)
    {
        for (var i = 0; i < _trapCount; i++)
        {
            var at = _traps + i * _d.Map.TrapSize;
            var bin = _d.U16(at + _d.Map.TrapBin);
            if (bin is < Career.FirstRank or > Career.LastRank)
            {
                continue;
            }
            if (bin > target)
            {
                if ((_d.U16(at + _d.Map.TrapFlags) & TrapCompleted) == 0)
                {
                    _d.U16(at + _d.Map.TrapFlags, TrapUnlocked | TrapCompleted);
                    _d.F32(at + _d.Map.TrapRecorded, _d.F32(at + _d.Map.TrapRequired));
                }
            }
            else
            {
                _d.U16(at + _d.Map.TrapFlags, bin == target ? TrapUnlocked : (ushort)0);
                _d.F32(at + _d.Map.TrapRecorded, 0f);
            }
        }
    }

    private void UpdateRaces(int target, bool movingBack)
    {
        foreach (var race in _races.Races.Where(r => r.IsBlacklist))
        {
            if (race.Group > target)
            {
                race.Flags |= RaceFlags.CareerCompleted | RaceFlags.CareerUnlocked | RaceFlags.QuickRaceUnlocked;
            }
            else if (movingBack)
            {
                race.Flags &= ~(RaceFlags.CareerCompleted | RaceFlags.CareerUnlocked);
            }
        }
    }

    private void UpdateBinStats(int target, bool movingBack)
    {
        foreach (var rival in GameData.Rivals)
        {
            if (!_binRows.TryGetValue(rival.Rank, out var row))
            {
                continue;
            }
            _d.U16(row, (ushort)CountChallenges(rival.Rank));
            if (rival.Rank > target)
            {
                _d.U16(row + 2, Math.Max(_d.U16(row + 2), (ushort)rival.RequiredWins));
            }
            else if (movingBack)
            {
                _d.U16(row + 2, 0);
            }
        }
    }

    private int CountChallenges(int rank)
    {
        var count = 0;
        for (var i = 0; i < _milestoneCount; i++)
        {
            var at = _milestones + i * MilestoneSize;
            count += _d.U16(at + MilestoneBin) == rank && _d[at + MilestoneState] == StateAwarded ? 1 : 0;
        }
        for (var i = 0; i < _trapCount; i++)
        {
            var at = _traps + i * _d.Map.TrapSize;
            count += _d.U16(at + _d.Map.TrapBin) == rank && (_d.U16(at + _d.Map.TrapFlags) & TrapCompleted) != 0 ? 1 : 0;
        }
        return count;
    }

    private static int Align(int absolute)
    {
        var relative = absolute - Layout.GameplayOffset;
        return ((relative + 15) & ~15) + Layout.GameplayOffset;
    }
}
