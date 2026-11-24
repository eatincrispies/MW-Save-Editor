namespace NFSMWSave.Core;

public enum ImpoundState
{
    None = 0,
    BustedByCross = 1,
    StrikeLimit = 2,
    NoFunds = 3,
    Released = 4,
}

public sealed class CareerCar
{
    public const int PerformanceSlots = 7;
    public const float MaxHeat = 10f;
    private const int FirstPowerAdderSlot = 5;
    private const int PartsOffset = 0x118;
    private const int JunkmanOffset = 0x134;
    private const int ImpoundMax = 2;
    private const int ImpoundTimes = 3;
    private const int ImpoundStateOffset = 4;
    private const int ImpoundDays = 5;
    private const int HeatOffset = 0xC;
    private const int BountyOffset = 0x10;
    private const int EvadedOffset = 0x14;
    private const int BustedOffset = 0x16;
    private const int UnservedOffset = 0x18;
    private const int ServedOffset = 0x28;
    private const int InfractionTypes = 8;
    private const int HistoryEvaded = 4;
    private const int HistoryBusted = 6;
    private const int HistoryUnserved = 8;
    private const int HistoryServed = 0x18;

    private readonly SaveBuffer _d;
    private readonly int _career;
    private readonly int _custom;

    internal CareerCar(SaveBuffer d, int slot, uint handle, uint feKey, uint vehicleKey, int career, int custom)
    {
        _d = d;
        Slot = slot;
        Handle = handle;
        FeKey = feKey;
        VehicleKey = vehicleKey;
        _career = career;
        _custom = custom;
        Info = GameData.FindCar(feKey);
    }

    public int Slot { get; }
    public uint Handle { get; }
    public uint FeKey { get; }
    public uint VehicleKey { get; }
    public CarInfo? Info { get; }
    public string Name => VehicleKey == CarCatalog.PrologueKey ? CarCatalog.PrologueName : CarCatalog.VehicleName(VehicleKey) ?? Info?.Name ?? CarCatalog.ChallengeName(VehicleKey, FeKey) ?? $"Special vehicle {VehicleKey:X8}";
    public bool HasCustomization => _custom >= 0;
    public bool IsCareer => _career >= 0;

    public float Heat
    {
        get => IsCareer ? _d.F32(_career + HeatOffset) : 0f;
        set
        {
            if (IsCareer)
            {
                _d.F32(_career + HeatOffset, Math.Clamp(value, 0f, MaxHeat));
            }
        }
    }

    public uint Bounty
    {
        get => IsCareer ? _d.U32(_career + BountyOffset) : 0;
        set
        {
            if (IsCareer)
            {
                _d.U32(_career + BountyOffset, Math.Min(value, CarStable.MaxBounty));
            }
        }
    }

    public ushort Evaded
    {
        get => IsCareer ? _d.U16(_career + EvadedOffset) : (ushort)0;
        set
        {
            if (IsCareer)
            {
                _d.U16(_career + EvadedOffset, value);
            }
        }
    }

    public ushort Busted
    {
        get => IsCareer ? _d.U16(_career + BustedOffset) : (ushort)0;
        set
        {
            if (IsCareer)
            {
                _d.U16(_career + BustedOffset, value);
            }
        }
    }

    public int MaxStrikes
    {
        get => IsCareer ? _d[_career + ImpoundMax] : 0;
        set
        {
            if (IsCareer)
            {
                _d[_career + ImpoundMax] = (byte)Math.Clamp(value, 1, 10);
            }
        }
    }

    public int Strikes
    {
        get => IsCareer ? _d[_career + ImpoundTimes] : 0;
        set
        {
            if (IsCareer)
            {
                _d[_career + ImpoundTimes] = (byte)Math.Clamp(value, 0, 10);
            }
        }
    }

    public ImpoundState Impound
    {
        get => IsCareer ? (ImpoundState)_d[_career + ImpoundStateOffset] : ImpoundState.None;
        set
        {
            if (IsCareer)
            {
                _d[_career + ImpoundStateOffset] = (byte)value;
            }
        }
    }

    public int ImpoundDaysLeft
    {
        get => IsCareer ? _d[_career + ImpoundDays] : 0;
        set
        {
            if (IsCareer)
            {
                _d[_career + ImpoundDays] = (byte)Math.Clamp(value, 0, 255);
            }
        }
    }

    public int UnservedInfractions
    {
        get
        {
            if (!IsCareer)
            {
                return 0;
            }
            var total = 0;
            for (var i = 0; i < InfractionTypes; i++)
            {
                total += _d.U16(_career + UnservedOffset + i * 2);
            }
            return total;
        }
    }

    public const int TuningSettings = 3;
    public const int TuningSliders = 7;
    public const int TuningLimit = 5;
    private const int TuningOffset = 0x138;
    private const int TuningSettingSize = 0x1C;
    private const int ActiveTuningOffset = 0x18C;
    private const float TuningStep = 0.2f;

    public static readonly IReadOnlyList<string> TuningNames = ["Steering", "Handling", "Brakes", "Ride Height", "Aerodynamics", "Nitrous", "Turbo / Supercharger"];

    public static readonly IReadOnlyList<string> TuningNeeds =
    [
        "Needs a Suspension upgrade",
        "Needs a Suspension upgrade",
        "Needs a Brakes upgrade",
        "Needs a Suspension upgrade",
        "Needs a body kit or spoiler",
        "Needs a Nitrous upgrade",
        "Needs a Turbo upgrade",
    ];

    public int ActiveTuning
    {
        get => HasCustomization ? Math.Clamp(_d.I32(_custom + ActiveTuningOffset), 0, TuningSettings - 1) : 0;
        set
        {
            if (HasCustomization)
            {
                _d.I32(_custom + ActiveTuningOffset, Math.Clamp(value, 0, TuningSettings - 1));
            }
        }
    }

    private int TuningAt(int setting, int slider) => _custom + TuningOffset + setting * TuningSettingSize + slider * 4;

    public int GetTuning(int setting, int slider)
    {
        if (!HasCustomization)
        {
            return 0;
        }
        var value = _d.F32(TuningAt(setting, slider));
        return float.IsFinite(value) ? (int)MathF.Round(Math.Clamp(value, -1f, 1f) / TuningStep) : 0;
    }

    public void SetTuning(int setting, int slider, int value)
    {
        if (HasCustomization)
        {
            _d.F32(TuningAt(setting, slider), Math.Clamp(value, -TuningLimit, TuningLimit) * TuningStep);
        }
    }

    internal float[] RawTuning
    {
        get
        {
            var values = new float[TuningSettings * TuningSliders];
            for (var i = 0; HasCustomization && i < values.Length; i++)
            {
                var value = _d.F32(TuningAt(i / TuningSliders, i % TuningSliders));
                values[i] = float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;
            }
            return values;
        }
    }

    internal void SetRawTuning(int active, IReadOnlyList<float> values)
    {
        if (!HasCustomization)
        {
            return;
        }
        ActiveTuning = active;
        for (var i = 0; i < TuningSettings * TuningSliders && i < values.Count; i++)
        {
            _d.F32(TuningAt(i / TuningSliders, i % TuningSliders), float.IsFinite(values[i]) ? Math.Clamp(values[i], -1f, 1f) : 0f);
        }
    }

    public static readonly IReadOnlyList<string> LevelTitles =["Stock", "Street", "Super Street", "Race", "Pro", "Super Pro", "Ultimate"];

    internal int[]? Limits { get; init; }

    public bool LimitsConfirmed => Limits is not null || Info is { LevelsConfirmed: true };

    public int MaxLevel(int slot)
    {
        var known = IsChallenge ? Limits?[slot] ?? 0 : Info is null || CarCatalog.VehicleName(VehicleKey) is not null ? 0 : Limits?[slot] ?? Info.MaxLevels[slot];
        return Math.Max(known, HasCustomization ? GetLevel(slot) : 0);
    }

    public IReadOnlyList<string> LevelNames(int slot)
    {
        var top = MaxLevel(slot);
        var packages = Limits is not null && Limits[slot] > 0 ? Limits[slot] : top;
        var names = new List<string> { LevelTitles[0] };
        for (var level = 1; level <= top; level++)
        {
            names.Add(LevelTitles[Math.Clamp(level + LevelTitles.Count - 1 - packages, 1, LevelTitles.Count - 1)]);
        }
        return names;
    }

    public int GetLevel(int slot) => _custom < 0 ? 0 : _d.I32(_custom + PartsOffset + slot * 4);

    public ushort[] InstalledParts()
    {
        var parts = new ushort[Layout.PartSlots];
        for (var i = 0; _custom >= 0 && i < parts.Length; i++)
        {
            parts[i] = _d.U16(_custom + i * 2);
        }
        return parts;
    }

    public void SetLevel(int slot, int level)
    {
        if (_custom < 0)
        {
            return;
        }
        var before = GetLevel(slot);
        var after = Math.Clamp(level, 0, MaxLevel(slot));
        _d.I32(_custom + PartsOffset + slot * 4, after);
        if (slot >= FirstPowerAdderSlot && before > 0 && after == 0)
        {
            SetJunkman(slot, false);
        }
    }

    public bool GetJunkman(int slot) => _custom >= 0 && (_d.U32(_custom + JunkmanOffset) & (1u << slot)) != 0;

    private bool IsChallenge => CarCatalog.IsChallengeCar(VehicleKey, FeKey);

    public bool CanSetJunkman(int slot)
    {
        if (_custom < 0)
        {
            return false;
        }
        if (GetJunkman(slot))
        {
            return true;
        }
        if (IsChallenge && MaxLevel(slot) == 0)
        {
            return false;
        }
        return slot < FirstPowerAdderSlot || GetLevel(slot) > 0;
    }

    public void SetJunkman(int slot, bool on)
    {
        if (_custom < 0 || (on && !CanSetJunkman(slot)))
        {
            return;
        }
        var value = _d.U32(_custom + JunkmanOffset);
        _d.U32(_custom + JunkmanOffset, on ? value | (1u << slot) : value & ~(1u << slot));
    }

    public ushort PaintPart => HasCustomization ? _d.U16(_custom + CarPaints.PaintSlot * 2) : CarPaints.NoPart;

    public ushort VinylPart => HasCustomization ? _d.U16(_custom + CarPaints.VinylSlot * 2) : CarPaints.NoPart;

    public ushort GetVinylColour(int index) => HasCustomization ? _d.U16(_custom + (CarPaints.VinylColourSlot + index) * 2) : CarPaints.NoPart;

    internal int CustomizationIndex { get; init; } = -1;

    internal ushort InstalledPart(int slot) => HasCustomization ? _d.U16(_custom + slot * 2) : CarPaints.NoPart;

    public ushort GetPart(int slot) => InstalledPart(slot);

    internal void SetBodyPart(int slot, ushort part)
    {
        if (!HasCustomization)
        {
            return;
        }
        var before = InstalledPart(slot);
        _d.U16(_custom + slot * 2, part);
        if (slot == CarParts.RimSlot && part != before && InstalledPart(CarParts.RearRimSlot) != CarPaints.NoPart)
        {
            _d.U16(_custom + CarParts.RearRimSlot * 2, part);
        }
    }

    internal void SetLook(ushort paint, ushort vinyl, IReadOnlyList<ushort> colours)
    {
        if (!HasCustomization)
        {
            return;
        }
        _d.U16(_custom + CarPaints.PaintSlot * 2, paint);
        _d.U16(_custom + CarPaints.VinylSlot * 2, vinyl);
        if (vinyl == CarPaints.NoPart)
        {
            return;
        }
        for (var i = 0; i < CarPaints.VinylColourSlots; i++)
        {
            var colour = i < colours.Count && colours[i] != CarPaints.NoPart ? colours[i] : GetVinylColour(i);
            _d.U16(_custom + (CarPaints.VinylColourSlot + i) * 2, colour != CarPaints.NoPart ? colour : CarPaints.StockColourParts[i]);
        }
    }

    internal int AllInfractions
    {
        get
        {
            if (!IsCareer)
            {
                return 0;
            }
            var total = 0;
            for (var i = 0; i < InfractionTypes; i++)
            {
                total += _d.U16(_career + UnservedOffset + i * 2) + _d.U16(_career + ServedOffset + i * 2);
            }
            return total;
        }
    }

    internal void MoveRecordToHistory(int history)
    {
        if (!IsCareer)
        {
            return;
        }
        _d.U32(history, (uint)Math.Min((long)_d.U32(history) + Bounty, CarStable.MaxBounty));
        AddCount(history + HistoryEvaded, Evaded);
        AddCount(history + HistoryBusted, Busted);
        for (var i = 0; i < InfractionTypes; i++)
        {
            AddCount(history + HistoryUnserved + i * 2, _d.U16(_career + UnservedOffset + i * 2));
            AddCount(history + HistoryServed + i * 2, _d.U16(_career + ServedOffset + i * 2));
        }
    }

    private void AddCount(int at, ushort value) => _d.U16(at, (ushort)Math.Min(_d.U16(at) + value, ushort.MaxValue));

    internal int[] UnservedCounts => InfractionCounts(UnservedOffset);

    internal int[] ServedCounts => InfractionCounts(ServedOffset);

    private int[] InfractionCounts(int offset)
    {
        var counts = new int[InfractionTypes];
        for (var i = 0; IsCareer && i < counts.Length; i++)
        {
            counts[i] = _d.U16(_career + offset + i * 2);
        }
        return counts;
    }

    internal void SetInfractionCounts(IReadOnlyList<int> unserved, IReadOnlyList<int> served)
    {
        for (var i = 0; IsCareer && i < InfractionTypes; i++)
        {
            _d.U16(_career + UnservedOffset + i * 2, (ushort)Math.Clamp(i < unserved.Count ? unserved[i] : 0, 0, ushort.MaxValue));
            _d.U16(_career + ServedOffset + i * 2, (ushort)Math.Clamp(i < served.Count ? served[i] : 0, 0, ushort.MaxValue));
        }
    }

    public void MaxPerformance()
    {
        for (var i = 0; i < PerformanceSlots; i++)
        {
            SetLevel(i, MaxLevel(i));
        }
    }

    public void ReleaseFromImpound()
    {
        Impound = ImpoundState.None;
        Strikes = 0;
        ImpoundDaysLeft = 0;
    }

    public void ClearInfractions()
    {
        if (!IsCareer)
        {
            return;
        }
        for (var i = 0; i < InfractionTypes; i++)
        {
            _d.U16(_career + UnservedOffset + i * 2, 0);
        }
    }
}

public sealed class CarStable
{
    public const uint MaxBounty = 2_000_000_000;
    private const int FeKeyOffset = 4;
    private const int VehicleKeyOffset = 8;
    private const int FilterOffset = 12;
    private const int CustomizationOffset = 16;
    private const int CareerOffset = 17;
    private const int PhysicsStart = 0x118;
    private const int DefaultStrikes = 3;
    private const int PackageSize = 0x20;
    private const int SoldEvaded = 4;
    private const int SoldBusted = 6;
    private const int SoldInfractions = 8;
    private const int SoldInfractionCount = 16;
    private const int LotReserve = 25;
    private const uint FilterLot = 1;
    private const uint FilterQuickRace = 4;
    private const uint FilterHidden = 0x20;
    private const uint FilterPinkSlip = 0x40;
    private const uint FilterLotAmerica = 0x10000 | FilterLot | FilterHidden;
    private const uint FilterNewMyCar = RegionMask | FilterQuickRace;
    public const int MaxMyCars = 20;
    private const int UpgradeBrakes = 1;
    private const int UpgradeChassis = 2;
    private const int UpgradeInduction = 5;
    private const int UpgradeNitrous = 6;
    private const uint RegionMask = 0xF0000;
    private const uint ImportFilterMask = RegionMask | 0x7F;

    private readonly SaveBuffer _d;
    private readonly Career _career;
    private readonly List<CareerCar> _cars = [];
    private readonly List<CareerCar> _myCars = [];
    private readonly HashSet<int> _repainted = [];
    private readonly HashSet<int> _deletedPresets = [];

    public CarStable(SaveBuffer d, Career career)
    {
        _d = d;
        _career = career;
        Reload();
    }

    public IReadOnlyList<CareerCar> Cars => _cars;

    public IReadOnlyList<CareerCar> MyCars => _myCars;

    public int MaxCars => _d.Map.CareerCount;

    public uint SoldHistoryBounty
    {
        get => _d.U32(_d.Map.CarDb + _d.Map.SoldBounty);
        private set => _d.U32(_d.Map.CarDb + _d.Map.SoldBounty, value);
    }

    public long TotalBounty => _cars.Sum(c => (long)c.Bounty) + SoldHistoryBounty;

    public long TotalEvaded => _cars.Sum(c => (long)c.Evaded) + _d.U16(_d.Map.CarDb + _d.Map.SoldBounty + SoldEvaded);

    public long TotalBusted => _cars.Sum(c => (long)c.Busted) + _d.U16(_d.Map.CarDb + _d.Map.SoldBounty + SoldBusted);

    public long TotalInfractions
    {
        get
        {
            long total = _cars.Sum(c => c.AllInfractions);
            var history = _d.Map.CarDb + _d.Map.SoldBounty + SoldInfractions;
            for (var i = 0; i < SoldInfractionCount; i++)
            {
                total += _d.U16(history + i * 2);
            }
            return total;
        }
    }

    public void SetTotalBounty(long wanted)
    {
        wanted = Math.Clamp(wanted, 0, MaxBounty);
        var delta = wanted - TotalBounty;
        var ordered = _cars.OrderByDescending(c => c.Handle == _career.CurrentCar).ToList();
        if (delta > 0)
        {
            if (ordered.Count == 0)
            {
                SoldHistoryBounty += (uint)delta;
                return;
            }
            foreach (var car in ordered)
            {
                var room = Math.Min(delta, MaxBounty - car.Bounty);
                car.Bounty += (uint)room;
                delta -= room;
                if (delta == 0)
                {
                    return;
                }
            }
            return;
        }
        var remove = -delta;
        foreach (var car in ordered)
        {
            var take = Math.Min(remove, car.Bounty);
            car.Bounty -= (uint)take;
            remove -= take;
        }
        if (remove > 0)
        {
            SoldHistoryBounty -= (uint)Math.Min(remove, SoldHistoryBounty);
        }
    }

    public string? CatalogLabel => CarCatalog.Choose(AllParts())?.Label;

    public int FreeGarageSlots => Math.Max(0, MaxCars - _cars.Count);

    public int FreeMyCarSlots => Math.Max(0, MaxMyCars - _myCars.Count);

    public IReadOnlyList<CatalogVehicle> AddableVehicles()
    {
        var family = _d.Map.CanAddCars ? CarCatalog.Choose(AllParts()) : null;
        return family is null ? [] : CarCatalog.WithPresets(family);
    }

    public string? DeleteProblem(IReadOnlyCollection<CareerCar> cars)
    {
        if (!_d.Map.CanAddCars)
        {
            return "Cars can't be deleted on this PS2 save version.";
        }
        var slots = cars.Select(c => c.Slot).ToHashSet();
        if (slots.Count == 0 || !_cars.Concat(_myCars).Any(c => slots.Contains(c.Slot)))
        {
            return "Select a car first.";
        }
        if (!_cars.Any(c => slots.Contains(c.Slot)))
        {
            return null;
        }
        return _cars.Any(c => !slots.Contains(c.Slot) && c.Impound == ImpoundState.None)
            ? null
            : "The game needs at least one car that isn't impounded, so this can't be deleted.";
    }

    public void DeleteCars(IReadOnlyCollection<CareerCar> cars)
    {
        var problem = DeleteProblem(cars);
        if (problem is not null)
        {
            throw new InvalidOperationException(problem);
        }
        var slots = cars.Select(c => c.Slot).ToHashSet();
        var keeper = _cars.FirstOrDefault(c => !slots.Contains(c.Slot) && c.Impound == ImpoundState.None);
        var deleted = new HashSet<uint>();
        foreach (var car in _cars.Concat(_myCars).Where(c => slots.Contains(c.Slot)).ToList())
        {
            car.MoveRecordToHistory(_d.Map.CarDb + _d.Map.SoldBounty);
            deleted.Add(car.Handle);
            FreeRecords(car);
        }
        if (keeper is not null && deleted.Contains(_career.CurrentCar))
        {
            _career.CurrentCar = keeper.Handle;
        }
        var fallback = QuickRaceFallback(keeper?.Handle ?? Layout.DefaultCarHandle);
        for (var i = 0; i < Layout.QuickRaceSlots; i++)
        {
            var at = _d.Map.QuickRaceCars + i * 4;
            if (deleted.Contains(_d.U32(at)))
            {
                _d.U32(at, fallback);
            }
        }
        Reload();
    }

    private void FreeRecords(CareerCar car)
    {
        var at = CarAt(car.Slot);
        var custom = _d[at + CustomizationOffset];
        var career = _d[at + CareerOffset];
        _d.U32(at, Layout.InvalidHandle);
        if (custom < _d.Map.CustomizationCount && !RecordUses(CustomizationOffset, custom))
        {
            _d[CustomAt(custom) + Layout.CustomizationHandle] = Layout.InvalidIndex;
            _deletedPresets.Add(custom);
        }
        if (career < _d.Map.CareerCount && !RecordUses(CareerOffset, career))
        {
            _d[CareerAt(career)] = Layout.InvalidIndex;
        }
        _repainted.Remove(car.Slot);
    }

    internal uint FilterOf(CareerCar car) => _d.U32(CarAt(car.Slot) + FilterOffset);

    internal CarHistory ReadHistory()
    {
        var at = _d.Map.CarDb + _d.Map.SoldBounty;
        var types = SoldInfractionCount / 2;
        var unserved = Enumerable.Range(0, types).Select(i => (int)_d.U16(at + SoldInfractions + i * 2)).ToArray();
        var served = Enumerable.Range(types, types).Select(i => (int)_d.U16(at + SoldInfractions + i * 2)).ToArray();
        return new CarHistory(_d.U32(at), _d.U16(at + SoldEvaded), _d.U16(at + SoldBusted), unserved, served);
    }

    private void WriteHistory(CarHistory history)
    {
        var at = _d.Map.CarDb + _d.Map.SoldBounty;
        var types = SoldInfractionCount / 2;
        _d.U32(at, Math.Min(history.Bounty, MaxBounty));
        _d.U16(at + SoldEvaded, (ushort)Math.Clamp(history.Evaded, 0, ushort.MaxValue));
        _d.U16(at + SoldBusted, (ushort)Math.Clamp(history.Busted, 0, ushort.MaxValue));
        for (var i = 0; i < types; i++)
        {
            _d.U16(at + SoldInfractions + i * 2, (ushort)Math.Clamp(i < history.Unserved.Length ? history.Unserved[i] : 0, 0, ushort.MaxValue));
            _d.U16(at + SoldInfractions + (types + i) * 2, (ushort)Math.Clamp(i < history.Served.Length ? history.Served[i] : 0, 0, ushort.MaxValue));
        }
    }

    internal string? GarageImportProblem()
    {
        if (!_d.Map.CanAddCars)
        {
            return "This PS2 save version can't receive cars.";
        }
        if (!_career.HasStarted)
        {
            return "Start a career in the game first.";
        }
        return CarCatalog.Choose(AllParts()) is null ? "This save uses a modded car list the editor doesn't recognize." : null;
    }

    internal (int Career, int Mine) ReplaceCars(IReadOnlyList<ImportedCar> career, IReadOnlyList<ImportedCar> mine, CarHistory? history, List<string> notes)
    {
        var family = CarCatalog.Choose(AllParts()) ?? throw new InvalidOperationException("This save uses a modded car list the editor doesn't recognize.");
        var known = CarCatalog.Vehicles(family).GroupBy(v => v.VehicleKey).ToDictionary(g => g.Key, g => g.First());
        var usable = career.Where(c => known.ContainsKey(CarCatalog.PartsKey(c.VehicleKey))).ToList();
        var usableMine = mine.Where(c => known.ContainsKey(CarCatalog.PartsKey(c.VehicleKey))).ToList();
        var unknown = career.Count + mine.Count - usable.Count - usableMine.Count;
        if (unknown > 0)
        {
            notes.Add($"{unknown} cars in the file don't exist in this save's car list and were left out.");
        }
        if (usable.Count == 0)
        {
            notes.Add("Cars were skipped because the file has no career car this save can use.");
            return (0, 0);
        }
        if (usable.Count > MaxCars)
        {
            throw new InvalidOperationException($"The file has more career cars than this save holds ({MaxCars}).");
        }
        var removed = new HashSet<uint>();
        foreach (var car in _cars.Concat(_myCars).ToList())
        {
            removed.Add(car.Handle);
            FreeRecords(car);
        }
        var placed = new List<(int Slot, ImportedCar Car)>();
        var resetParts = 0;
        foreach (var car in usable)
        {
            var slot = PlaceCar(family, known[CarCatalog.PartsKey(car.VehicleKey)], car, true, ref resetParts);
            if (slot < 0)
            {
                throw new InvalidOperationException("This save has no free car records left.");
            }
            placed.Add((slot, car));
        }
        var skipped = 0;
        foreach (var car in usableMine)
        {
            var slot = PlaceCar(family, known[CarCatalog.PartsKey(car.VehicleKey)], car, false, ref resetParts);
            if (slot < 0)
            {
                skipped++;
                continue;
            }
            placed.Add((slot, car));
        }
        if (skipped > 0)
        {
            notes.Add($"{skipped} My Cars didn't fit in this save's free car records.");
        }
        if (resetParts > 0)
        {
            notes.Add($"{resetParts} parts don't exist on this platform and went back to stock.");
        }
        Reload();
        foreach (var (slot, source) in placed)
        {
            var target = _cars.Concat(_myCars).First(c => c.Slot == slot);
            for (var i = 0; i < CareerCar.PerformanceSlots && i < source.Upgrades.Count; i++)
            {
                target.SetLevel(i, source.Upgrades[i].Level);
            }
            for (var i = 0; i < CareerCar.PerformanceSlots && i < source.Upgrades.Count; i++)
            {
                target.SetJunkman(i, source.Upgrades[i].Junkman);
            }
            if (source.Tuning is { } tuning)
            {
                target.SetRawTuning(tuning.Active, tuning.Values);
            }
            if (source.Police is { } police)
            {
                target.Heat = police.Heat;
                target.Bounty = police.Bounty;
                target.Evaded = (ushort)Math.Clamp(police.Evaded, 0, ushort.MaxValue);
                target.Busted = (ushort)Math.Clamp(police.Busted, 0, ushort.MaxValue);
                target.MaxStrikes = police.MaxStrikes;
                target.Strikes = police.Strikes;
                target.Impound = police.Impound;
                target.ImpoundDaysLeft = police.ImpoundDays;
                target.SetInfractionCounts(police.Unserved, police.Served);
            }
            _repainted.Add(slot);
        }
        if (_cars.All(c => c.Impound != ImpoundState.None))
        {
            _cars[0].ReleaseFromImpound();
            notes.Add($"Every car was impounded, so {_cars[0].Name} was released. The game needs one free car.");
        }
        var currentSlot = placed.Where(p => p.Car.IsCurrent).Select(p => p.Slot).FirstOrDefault(-1);
        var current = _cars.FirstOrDefault(c => c.Slot == currentSlot && c.Impound == ImpoundState.None) ?? _cars.First(c => c.Impound == ImpoundState.None);
        _career.CurrentCar = current.Handle;
        var fallback = QuickRaceFallback(current.Handle);
        for (var i = 0; i < Layout.QuickRaceSlots; i++)
        {
            var at = _d.Map.QuickRaceCars + i * 4;
            if (removed.Contains(_d.U32(at)))
            {
                _d.U32(at, fallback);
            }
        }
        if (history is not null)
        {
            WriteHistory(history);
        }
        return (_cars.Count, _myCars.Count);
    }

    private int PlaceCar(CatalogFamily family, CatalogVehicle stock, ImportedCar car, bool isCareer, ref int resetParts)
    {
        var slot = FreeCarSlot();
        var customIndex = FreeCustomization();
        var careerIndex = isCareer ? FreeCareer() : -1;
        if (slot < 0 || customIndex < 0 || (isCareer && careerIndex < 0))
        {
            return -1;
        }
        var record = CustomAt(customIndex);
        var parts = new ushort[Layout.PartSlots];
        Array.Fill(parts, CarPaints.NoPart);
        foreach (var part in car.Parts)
        {
            var match = CarParts.MatchPart(family, part.Slot, part.Part, part.Hash);
            resetParts += match is null ? 1 : 0;
            parts[part.Slot] = match ?? stock.Parts[part.Slot];
        }
        if (parts[CarParts.BodySlot] == CarPaints.NoPart)
        {
            parts[CarParts.BodySlot] = stock.Parts[CarParts.BodySlot];
        }
        for (var i = 0; i < Layout.PartSlots; i++)
        {
            _d.U16(record + i * 2, parts[i]);
        }
        _d.Data.AsSpan(record + PhysicsStart, Layout.CustomizationHandle - PhysicsStart).Clear();
        _d[record + Layout.CustomizationHandle] = (byte)customIndex;
        if (isCareer)
        {
            var careerAt = CareerAt(careerIndex);
            _d.Data.AsSpan(careerAt, Layout.CareerSize).Clear();
            _d[careerAt] = (byte)careerIndex;
            _d[careerAt + 2] = DefaultStrikes;
        }
        var filter = car.Filter & ImportFilterMask & ~FilterLot;
        filter = isCareer ? (filter & ~FilterQuickRace) | Layout.FilterCareer : (filter & ~Layout.FilterCareer) | FilterQuickRace;
        if ((filter & RegionMask) == 0)
        {
            filter |= RegionMask;
        }
        var to = CarAt(slot);
        _d.U32(to, (uint)slot);
        _d.U32(to + FeKeyOffset, car.FeKey == stock.FeKey || GameData.FindCar(car.FeKey) is not null ? car.FeKey : stock.FeKey);
        _d.U32(to + VehicleKeyOffset, car.VehicleKey);
        _d.U32(to + FilterOffset, filter);
        _d[to + CustomizationOffset] = (byte)customIndex;
        _d[to + CareerOffset] = isCareer ? (byte)careerIndex : Layout.InvalidIndex;
        return slot;
    }

    internal (int[] Repainted, int[] Deleted) PendingState() => (_repainted.ToArray(), _deletedPresets.ToArray());

    internal void RestorePending((int[] Repainted, int[] Deleted) state)
    {
        _repainted.Clear();
        _repainted.UnionWith(state.Repainted);
        _deletedPresets.Clear();
        _deletedPresets.UnionWith(state.Deleted);
        Reload();
    }

    internal IReadOnlyList<int> TakeDeletedPresets()
    {
        var list = _deletedPresets.ToList();
        _deletedPresets.Clear();
        return list;
    }

    private bool RecordUses(int field, byte index)
    {
        for (var slot = 0; slot < Layout.CarCount; slot++)
        {
            var at = CarAt(slot);
            if (_d.U32(at) != Layout.InvalidHandle && _d[at + field] == index)
            {
                return true;
            }
        }
        return false;
    }

    private uint QuickRaceFallback(uint keeper)
    {
        var stock = keeper;
        for (var slot = Layout.CarCount - 1; slot >= 0; slot--)
        {
            var at = CarAt(slot);
            var handle = _d.U32(at);
            if (handle == Layout.DefaultCarHandle)
            {
                return handle;
            }
            if (handle != Layout.InvalidHandle && (_d.U32(at + FilterOffset) & FilterLot) != 0)
            {
                stock = handle;
            }
        }
        return stock;
    }

    public string? LotProblem()
    {
        if (!_d.Map.CanAddCars)
        {
            return "The Car Lot can't be edited for this PS2 save version.";
        }
        if (!_career.HasStarted)
        {
            return "Start a career in the game before editing the Car Lot.";
        }
        return CarCatalog.Choose(AllParts()) is null ? "This save uses a modded car list the editor doesn't recognize." : null;
    }

    public IReadOnlyList<LotChoice> LotChoices()
    {
        var family = LotProblem() is null ? CarCatalog.Choose(AllParts()) : null;
        if (family is null)
        {
            return [];
        }
        var choices = new List<LotChoice>();
        foreach (var vehicle in CarCatalog.Vehicles(family).Where(v => v.Safety != VehicleSafety.MayCrash))
        {
            var slot = LotRecord(vehicle.VehicleKey);
            var filter = slot >= 0 ? _d.U32(CarAt(slot) + FilterOffset) : 0;
            if (CarCatalog.IsAddon(vehicle.VehicleKey) && (filter & FilterLot) == 0)
            {
                continue;
            }
            if (slot >= 0 && (filter & (FilterLot | FilterHidden)) == FilterLot && _d.U32(CarAt(slot) + FeKeyOffset) != CarCatalog.GenericFrontendKey)
            {
                continue;
            }
            choices.Add(new LotChoice(vehicle, slot >= 0 && (filter & FilterLot) != 0));
        }
        return choices;
    }

    public bool SetLot(IReadOnlyCollection<CatalogVehicle> wanted)
    {
        var problem = LotProblem();
        if (problem is not null)
        {
            throw new InvalidOperationException(problem);
        }
        var keys = wanted.Select(v => v.VehicleKey).ToHashSet();
        var choices = LotChoices();
        var needed = choices.Count(c => keys.Contains(c.Vehicle.VehicleKey) && LotRecord(c.Vehicle.VehicleKey) < 0);
        if (FreeCarRecords() - needed < LotReserve)
        {
            throw new InvalidOperationException("This save doesn't have enough free car records for that many vehicles.");
        }
        var changed = false;
        foreach (var choice in choices)
        {
            var on = keys.Contains(choice.Vehicle.VehicleKey);
            if (on == choice.InLot)
            {
                continue;
            }
            var slot = LotRecord(choice.Vehicle.VehicleKey);
            if (slot < 0)
            {
                slot = FreeCarSlot();
                var to = CarAt(slot);
                _d.U32(to, (uint)slot);
                _d.U32(to + FeKeyOffset, choice.Vehicle.FeKey);
                _d.U32(to + VehicleKeyOffset, choice.Vehicle.VehicleKey);
                _d.U32(to + FilterOffset, FilterLotAmerica);
                _d[to + CustomizationOffset] = Layout.InvalidIndex;
                _d[to + CareerOffset] = Layout.InvalidIndex;
            }
            else
            {
                var at = CarAt(slot) + FilterOffset;
                var filter = _d.U32(at);
                _d.U32(at, on ? filter | FilterLot | FilterHidden : (filter & ~FilterLot) | FilterHidden);
            }
            changed = true;
        }
        return changed;
    }

    private int LotRecord(uint vehicleKey)
    {
        for (var slot = 0; slot < Layout.CarCount; slot++)
        {
            var at = CarAt(slot);
            if (_d.U32(at) != Layout.InvalidHandle && _d.U32(at + VehicleKeyOffset) == vehicleKey && _d[at + CustomizationOffset] == Layout.InvalidIndex && _d[at + CareerOffset] == Layout.InvalidIndex)
            {
                return slot;
            }
        }
        return -1;
    }

    private int FreeCarRecords()
    {
        var count = 0;
        for (var slot = 0; slot < Layout.CarCount; slot++)
        {
            if (_d.U32(CarAt(slot)) == Layout.InvalidHandle)
            {
                count++;
            }
        }
        return count;
    }

    private int PresetSource(CatalogVehicle vehicle)
    {
        for (var slot = 0; slot < Layout.CarCount; slot++)
        {
            var at = CarAt(slot);
            var custom = _d[at + CustomizationOffset];
            if (_d.U32(at) == vehicle.PresetHash && _d.U32(at + VehicleKeyOffset) == vehicle.VehicleKey && custom < _d.Map.CustomizationCount && _d[CustomAt(custom) + Layout.CustomizationHandle] == custom)
            {
                return CustomAt(custom);
            }
        }
        return -1;
    }

    internal IEnumerable<CareerCar> RepaintedCars => _cars.Concat(_myCars).Where(c => _repainted.Contains(c.Slot));

    public void SetLook(CareerCar car, ushort paint, ushort vinyl, IReadOnlyList<ushort> colours)
    {
        car.SetLook(paint, vinyl, colours);
        _repainted.Add(car.Slot);
    }

    public bool CanChangeParts(CareerCar car)
    {
        if (!_d.Map.CanAddCars || !car.HasCustomization)
        {
            return false;
        }
        var family = CarCatalog.Choose(AllParts());
        return family is not null && CarParts.Kinds.All(k => CarParts.For(family, car.VehicleKey, k.Slot).Count > 0);
    }

    public IReadOnlyList<PartChoice> PartsFor(CareerCar car, int slot)
    {
        var family = _d.Map.CanAddCars ? CarCatalog.Choose(AllParts()) : null;
        return family is null ? [] : CarParts.For(family, car.VehicleKey, slot);
    }

    public void SetParts(CareerCar car, IReadOnlyDictionary<int, ushort> parts)
    {
        foreach (var (slot, part) in parts)
        {
            var fits = slot == CarPaints.RimPaintSlot
                ? part == CarPaints.NoPart || CarPaints.RimPaints.Any(p => p.Part == part)
                : PartsFor(car, slot).Any(p => p.Part == part);
            if (part != car.GetPart(slot) && !fits)
            {
                throw new InvalidOperationException("That part does not fit this car.");
            }
        }
        foreach (var (slot, part) in parts)
        {
            car.SetBodyPart(slot, part);
        }
        if (IsStockRim(car, car.GetPart(CarParts.RimSlot)))
        {
            car.SetBodyPart(CarPaints.RimPaintSlot, CarPaints.NoPart);
        }
        _repainted.Add(car.Slot);
    }

    public bool CanTune(CareerCar car, int slider)
    {
        if (!car.HasCustomization)
        {
            return false;
        }
        return slider switch
        {
            0 or 1 or 3 => car.GetLevel(UpgradeChassis) > 0,
            2 => car.GetLevel(UpgradeBrakes) > 0,
            4 => IsCustomPart(car, CarParts.SpoilerSlot) || IsCustomPart(car, CarParts.BodySlot),
            5 => car.GetLevel(UpgradeNitrous) > 0,
            6 => car.GetLevel(UpgradeInduction) > 0,
            _ => false,
        };
    }

    private bool IsCustomPart(CareerCar car, int slot)
    {
        var list = PartsFor(car, slot);
        var part = car.GetPart(slot);
        return list.Count > 0 && part != CarPaints.NoPart && part != list[0].Part;
    }

    public bool IsStockRim(CareerCar car, ushort rim)
    {
        var list = PartsFor(car, CarParts.RimSlot);
        return list.Count > 0 && list[0].Part == rim;
    }

    internal Func<ushort, uint?> PresetHashes()
    {
        var family = _d.Map.CanAddCars ? CarCatalog.Choose(AllParts()) : null;
        return part => CarPaints.NameHash(part) ?? (family is null ? null : CarParts.NameHash(family, part));
    }

    public CarVisualChoices? VisualChoices(CareerCar car)
    {
        if (!_d.Map.CanAddCars || !car.HasCustomization)
        {
            return null;
        }
        var family = CarCatalog.Choose(AllParts());
        return family is null ? null : CarParts.Visuals(family);
    }

    public void SetVisual(CareerCar car, int slot, ushort part)
    {
        var choices = VisualChoices(car) ?? throw new InvalidOperationException("Visuals can't be changed on this car.");
        var fits = slot switch
        {
            CarParts.WindowTintSlot => choices.Tints.Any(p => p.Part == part),
            CarParts.GaugeSlot => choices.Gauges.Any(p => p.Part == part),
            CarParts.GaugeFaceSlot or CarParts.GaugeNeedleSlot or CarParts.GaugeLetteringSlot => choices.GaugeColours.Any(p => p.Part == part),
            _ => CarParts.DecalPlaces.Any(p => slot >= p.FirstSlot && slot < p.FirstSlot + p.SlotCount) && (part == CarPaints.NoPart || choices.Decals.Any(p => p.Part == part)),
        };
        if (!fits)
        {
            throw new InvalidOperationException("That choice does not fit this car.");
        }
        car.SetBodyPart(slot, part);
        _repainted.Add(car.Slot);
    }

    public void SetNumbers(CareerCar car, ushort left, ushort right)
    {
        var choices = VisualChoices(car) ?? throw new InvalidOperationException("Numbers can't be changed on this car.");
        var clear = left == CarPaints.NoPart || right == CarPaints.NoPart;
        if (!clear && (choices.LeftNumbers.All(p => p.Part != left) || choices.RightNumbers.All(p => p.Part != right)))
        {
            throw new InvalidOperationException("Those numbers do not fit this car.");
        }
        var leftPart = clear ? CarPaints.NoPart : left;
        var rightPart = clear ? CarPaints.NoPart : right;
        car.SetBodyPart(CarParts.RightDoorLeftNumber, leftPart);
        car.SetBodyPart(CarParts.RightDoorRightNumber, rightPart);
        car.SetBodyPart(CarParts.LeftDoorLeftNumber, leftPart);
        car.SetBodyPart(CarParts.LeftDoorRightNumber, rightPart);
        _repainted.Add(car.Slot);
    }

    public bool CanPaint(CareerCar car)
    {
        if (!_d.Map.CanAddCars || !car.HasCustomization)
        {
            return false;
        }
        var family = CarCatalog.Choose(AllParts());
        return family is not null && CarPaints.FitsFamily(family);
    }

    public string? AddProblem(bool myCars = false)
    {
        if (!_d.Map.CanAddCars)
        {
            return "Adding cars is not available for this PS2 save version.";
        }
        if (myCars)
        {
            if (_myCars.Count >= MaxMyCars)
            {
                return $"My Cars is full ({MaxMyCars} cars).";
            }
            if (FreeCarSlot() < 0 || FreeCustomization() < 0)
            {
                return "This save has no free car records left.";
            }
        }
        else
        {
            if (!_career.HasStarted)
            {
                return "Start a career in the game before adding cars.";
            }
            if (_cars.Count >= MaxCars)
            {
                return $"The garage is full ({MaxCars} cars).";
            }
            if (FreeCarSlot() < 0 || FreeCustomization() < 0 || FreeCareer() < 0)
            {
                return "This save has no free car records left.";
            }
        }
        return CarCatalog.Choose(AllParts()) is null ? "This save uses a modded car list the editor doesn't recognize." : null;
    }

    public CareerCar AddVehicle(CatalogVehicle vehicle, bool myCars = false)
    {
        var problem = AddProblem(myCars);
        if (problem is not null)
        {
            throw new InvalidOperationException(problem);
        }
        var slot = FreeCarSlot();
        var customIndex = FreeCustomization();
        var careerIndex = myCars ? -1 : FreeCareer();

        var record = CustomAt(customIndex);
        var isPreset = vehicle.PresetHash != 0;
        var source = isPreset ? PresetSource(vehicle) : -1;
        for (var i = 0; i < Layout.PartSlots; i++)
        {
            _d.U16(record + i * 2, source >= 0 ? _d.U16(source + i * 2) : vehicle.Parts[i]);
        }
        _d.Data.AsSpan(record + PhysicsStart, Layout.CustomizationHandle - PhysicsStart).Clear();
        if (source >= 0)
        {
            _d.Data.AsSpan(source + PhysicsStart, PackageSize).CopyTo(_d.Data.AsSpan(record + PhysicsStart));
        }
        else if (vehicle.Levels is not null)
        {
            var info = GameData.FindCar(vehicle.FeKey);
            var family = CarCatalog.Choose(AllParts());
            var limits = family is null ? null : CarParts.UpgradeLimits(family, vehicle.VehicleKey);
            for (var i = 0; i < CareerCar.PerformanceSlots; i++)
            {
                _d.I32(record + PhysicsStart + i * 4, info is null || CarCatalog.VehicleName(vehicle.VehicleKey) is not null ? 0 : Math.Min(vehicle.Levels[i], limits?[i] ?? info.MaxLevels[i]));
            }
        }
        _d[record + Layout.CustomizationHandle] = (byte)customIndex;

        if (!myCars)
        {
            var career = CareerAt(careerIndex);
            _d.Data.AsSpan(career, Layout.CareerSize).Clear();
            _d[career] = (byte)careerIndex;
            _d[career + 2] = DefaultStrikes;
        }

        var to = CarAt(slot);
        _d.U32(to, (uint)slot);
        _d.U32(to + FeKeyOffset, vehicle.FeKey);
        _d.U32(to + VehicleKeyOffset, vehicle.VehicleKey);
        _d.U32(to + FilterOffset, myCars ? FilterNewMyCar : isPreset ? Layout.FilterNewCareerCar | FilterPinkSlip : Layout.FilterNewCareerCar);
        _d[to + CustomizationOffset] = (byte)customIndex;
        _d[to + CareerOffset] = myCars ? Layout.InvalidIndex : (byte)careerIndex;
        if (isPreset)
        {
            _repainted.Add(slot);
        }
        Reload();
        return (myCars ? _myCars : _cars).First(c => c.Slot == slot);
    }

    private void Reload()
    {
        _cars.Clear();
        _myCars.Clear();
        var family = _d.Map.CanAddCars ? CarCatalog.Choose(AllParts()) : null;
        for (var slot = 0; slot < Layout.CarCount; slot++)
        {
            var at = CarAt(slot);
            var handle = _d.U32(at);
            if (handle == Layout.InvalidHandle)
            {
                continue;
            }
            var filter = _d.U32(at + FilterOffset);
            var career = _d[at + CareerOffset];
            var custom = _d[at + CustomizationOffset];
            var customAt = custom < _d.Map.CustomizationCount ? CustomAt(custom) : -1;
            if ((filter & Layout.FilterCareer) != 0 && career < _d.Map.CareerCount)
            {
                _cars.Add(new CareerCar(_d, slot, handle, _d.U32(at + FeKeyOffset), _d.U32(at + VehicleKeyOffset), CareerAt(career), customAt) { CustomizationIndex = customAt >= 0 ? custom : -1, Limits = family is null ? null : CarParts.UpgradeLimits(family, _d.U32(at + VehicleKeyOffset)) });
            }
            else if ((filter & FilterQuickRace) != 0 && customAt >= 0)
            {
                _myCars.Add(new CareerCar(_d, slot, handle, _d.U32(at + FeKeyOffset), _d.U32(at + VehicleKeyOffset), -1, customAt) { CustomizationIndex = custom, Limits = family is null ? null : CarParts.UpgradeLimits(family, _d.U32(at + VehicleKeyOffset)) });
            }
        }
    }

    private IEnumerable<ushort[]> AllParts()
    {
        for (var i = 0; i < _d.Map.CustomizationCount; i++)
        {
            var record = CustomAt(i);
            if (_d[record + Layout.CustomizationHandle] != i)
            {
                continue;
            }
            var parts = new ushort[Layout.PartSlots];
            for (var slot = 0; slot < parts.Length; slot++)
            {
                parts[slot] = _d.U16(record + slot * 2);
            }
            yield return parts;
        }
    }

    private int FreeCarSlot()
    {
        for (var slot = 0; slot < Layout.CarCount; slot++)
        {
            if (_d.U32(CarAt(slot)) == Layout.InvalidHandle)
            {
                return slot;
            }
        }
        return -1;
    }

    private int FreeCustomization()
    {
        for (var i = 0; i < _d.Map.CustomizationCount; i++)
        {
            if (_d[CustomAt(i) + Layout.CustomizationHandle] == Layout.InvalidIndex)
            {
                return i;
            }
        }
        return -1;
    }

    private int FreeCareer()
    {
        for (var i = 0; i < _d.Map.CareerCount; i++)
        {
            if (_d[CareerAt(i)] == Layout.InvalidIndex)
            {
                return i;
            }
        }
        return -1;
    }

    private int CarAt(int slot) => _d.Map.CarDb + slot * Layout.CarSize;
    private int CustomAt(int index) => _d.Map.CarDb + Layout.CustomizationTable + index * Layout.CustomizationSize;
    private int CareerAt(int index) => _d.Map.CarDb + _d.Map.CareerTable + index * Layout.CareerSize;
}
