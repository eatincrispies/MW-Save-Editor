namespace NFSMWSave.Core;

public sealed record BinStatus(int Rank, int Challenges, int RaceWins, int MilestonesDone, int MilestonesTotal);

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
    private const ushort TrapCompleted = 4;
    private const int MaxBins = 32;

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
