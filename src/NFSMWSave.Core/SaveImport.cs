using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace NFSMWSave.Core;

[Flags]
public enum ImportSections
{
    None = 0,
    Alias = 1,
    Money = 2,
    Progress = 4,
    Cars = 8,
    Markers = 16,
    RapSheet = 32,
    Options = 64,
}

public sealed record ImportReport(int CareerCars, int MyCars, int Races, IReadOnlyList<string> Notes);

internal sealed record ProgressState(int State, float Recorded);

internal sealed record ImportedBin(int Rank, int Challenges, int RaceWins, IReadOnlyList<ProgressState> Milestones, IReadOnlyList<ProgressState> Traps);

internal sealed record ImportedPart(int Slot, ushort Part, uint? Hash);

internal sealed record ImportedUpgrade(int Level, bool Junkman);

internal sealed record ImportedPolice(float Heat, uint Bounty, int Evaded, int Busted, int Strikes, int MaxStrikes, ImpoundState Impound, int ImpoundDays, int[] Unserved, int[] Served);

internal sealed record ImportedCar(string Name, uint VehicleKey, uint FeKey, uint Filter, bool IsCurrent, ImportedPolice? Police, IReadOnlyList<ImportedUpgrade> Upgrades, IReadOnlyList<ImportedPart> Parts);

internal sealed record CarHistory(uint Bounty, int Evaded, int Busted, int[] Unserved, int[] Served);

public sealed class SaveImport
{
    private const long MaxFileBytes = 16 * 1024 * 1024;
    private const RaceFlags KnownRaceFlags = RaceFlags.QuickRaceCompleted | RaceFlags.CareerCompleted | RaceFlags.QuickRaceUnlocked | RaceFlags.CareerUnlocked | RaceFlags.OnlineUnlocked;
    private const CareerFlags EndgameFlags = CareerFlags.BeatenCareer | CareerFlags.BeatenChallengeSeries | CareerFlags.BeatenSpecialChallenge;

    private static readonly HashSet<string> ListNames =
        ["blacklist", "careerCars", "myCars", "races", "markers", "unlocks", "upgrades", "parts", "milestones", "traps", "unserved", "served"];

    private readonly JsonNode _root;
    private readonly JsonNode _career;
    private readonly IReadOnlyList<ImportedCar> _careerCars;
    private readonly IReadOnlyList<ImportedCar> _myCars;
    private readonly IReadOnlyList<ImportedBin> _bins;
    private readonly CarHistory? _history;

    private SaveImport(JsonNode root)
    {
        if (Str(root, "format") != SaveExport.FormatName)
        {
            throw new FormatException("format");
        }
        var version = Int(root, "formatVersion");
        if (version < SaveExport.FormatVersion)
        {
            throw new SaveFormatException("This export was made by an older version of the editor. Open the original save and export it again.");
        }
        if (version > SaveExport.FormatVersion)
        {
            throw new SaveFormatException("This export was made by a newer version of the editor.");
        }
        _root = root;
        _career = Need(root, "career");
        Platform = Str(Need(root, "source"), "platform");
        Alias = Str(_career, "alias");
        RankName = Str(_career, "rankName");
        Cash = Long(_career, "cash");
        _careerCars = Items(root["careerCars"]).Select(ParseCar).ToList();
        _myCars = Items(root["myCars"]).Select(ParseCar).ToList();
        _bins = Items(root["blacklist"]).Select(ParseBin).ToList();
        RaceCount = Items(root["races"]).Count();
        _history = root["soldHistory"] is { } history
            ? new CarHistory(UInt(history, "bounty"), Int(history, "evaded"), Int(history, "busted"), Counts(history, "unserved"), Counts(history, "served"))
            : null;
    }

    public string Platform { get; }
    public string Alias { get; }
    public string RankName { get; }
    public long Cash { get; }
    public int CareerCarCount => _careerCars.Count;
    public int MyCarCount => _myCars.Count;
    public int RaceCount { get; }

    public static SaveImport Load(string path)
    {
        if (new FileInfo(path).Length > MaxFileBytes)
        {
            throw new SaveFormatException("This file is too large to be a save export.");
        }
        var text = File.ReadAllText(path);
        try
        {
            return new SaveImport(text.AsSpan().TrimStart().StartsWith("<") ? ParseXml(text) : JsonNode.Parse(text) ?? throw new FormatException("empty"));
        }
        catch (Exception ex) when (ex is JsonException or XmlException or FormatException or OverflowException or InvalidOperationException or ArgumentException)
        {
            throw new SaveFormatException("This file isn't a valid NFSMW save export.");
        }
    }

    internal ImportReport Apply(SaveFile save, ImportSections sections)
    {
        var notes = new List<string>();
        var careerCars = 0;
        var myCars = 0;
        var races = 0;
        var bountyBefore = save.Stable.TotalBounty;
        if (sections.HasFlag(ImportSections.Alias))
        {
            if (!save.Map.CanRenameAlias)
            {
                notes.Add("Original Xbox saves keep their alias, because the save's folder uses it too.");
            }
            else if (SaveFile.AliasProblem(Alias) is null)
            {
                save.SetAlias(Alias);
            }
            else
            {
                notes.Add("The alias in the file isn't valid, so it was kept.");
            }
        }
        if (sections.HasFlag(ImportSections.Progress))
        {
            races = ApplyProgress(save, notes);
        }
        if (sections.HasFlag(ImportSections.Cars))
        {
            var problem = save.Stable.GarageImportProblem();
            if (problem is null)
            {
                (careerCars, myCars) = save.Stable.ReplaceCars(_careerCars, _myCars, _history, notes);
                if (!sections.HasFlag(ImportSections.Money))
                {
                    save.Stable.SetTotalBounty(bountyBefore);
                }
            }
            else
            {
                notes.Add($"Cars were skipped. {problem}");
            }
        }
        if (sections.HasFlag(ImportSections.Money))
        {
            save.Career.Cash = (uint)Math.Clamp(Cash, 0, Career.MaxCash);
            save.Stable.SetTotalBounty(Long(_career, "totalBounty"));
        }
        if (sections.HasFlag(ImportSections.Markers))
        {
            ApplyMarkers(save, notes);
        }
        if (sections.HasFlag(ImportSections.RapSheet))
        {
            ApplyRapSheet(save, notes);
        }
        if (sections.HasFlag(ImportSections.Options))
        {
            ApplyOptions(save, notes);
        }
        return new ImportReport(careerCars, myCars, races, notes);
    }

    private int ApplyProgress(SaveFile save, List<string> notes)
    {
        var rank = Int(_career, "rank");
        if (rank is < Career.FirstRank or > Career.LastRank)
        {
            notes.Add("Blacklist and races were skipped because the file's career is still in the prologue.");
            return 0;
        }
        if (!save.Progress.CanChangeRank)
        {
            notes.Add("Blacklist and races were skipped. Finish the prologue in this save first.");
            return 0;
        }
        save.Progress.SetRank(rank);
        var mismatched = _bins.Count(bin => !save.Progress.ApplyBin(bin));
        if (mismatched > 0)
        {
            notes.Add($"{mismatched} Blacklist ranks have a different challenge list on this platform, so their challenges were set from the rank only.");
        }
        var byId = new Dictionary<string, JsonNode>();
        foreach (var race in Items(_root["races"]))
        {
            byId[Str(race, "id")] = race;
        }
        var matched = 0;
        foreach (var race in save.Races.Races)
        {
            if (!byId.TryGetValue(race.Id, out var node))
            {
                continue;
            }
            var flags = race.Flags & ~KnownRaceFlags;
            flags |= Bool(node, "careerUnlocked") ? RaceFlags.CareerUnlocked : 0;
            flags |= Bool(node, "careerCompleted") ? RaceFlags.CareerCompleted : 0;
            flags |= Bool(node, "quickRaceUnlocked") ? RaceFlags.QuickRaceUnlocked : 0;
            flags |= Bool(node, "quickRaceCompleted") ? RaceFlags.QuickRaceCompleted : 0;
            flags |= Bool(node, "onlineUnlocked") ? RaceFlags.OnlineUnlocked : 0;
            race.Flags = flags;
            matched++;
        }
        if (matched < byId.Count)
        {
            notes.Add($"{byId.Count - matched} races in the file don't exist on this platform.");
        }
        var saved = Need(_career, "flags");
        var current = save.Career.Flags;
        foreach (var flag in Enum.GetValues<CareerFlags>())
        {
            if (flag == CareerFlags.CareerStarted || ((flag & EndgameFlags) != 0 && !save.Map.HasEndgameFlags))
            {
                continue;
            }
            current = Bool(saved, SaveExport.Key(flag.ToString())) ? current | flag : current & ~flag;
        }
        save.Career.Flags = current;
        save.Career.CompletedOnce = Bool(saved, "completedOnce");
        foreach (var unlock in Items(_root["unlocks"]))
        {
            save.Unlocks.SetLevel(Int(unlock, "entity"), Int(unlock, "level"));
        }
        return matched;
    }

    private void ApplyMarkers(SaveFile save, List<string> notes)
    {
        var wanted = Items(_root["markers"]).ToDictionary(m => Int(m, "id"), m => Math.Max(Int(m, "count"), 0));
        var known = MarkerInventory.Types.Where(t => wanted.ContainsKey(t.Id)).ToList();
        foreach (var type in known.Where(t => wanted[t.Id] < save.Markers.Count(t.Id)))
        {
            save.Markers.SetCount(type.Id, wanted[type.Id]);
        }
        var missing = known.Count(type => save.Markers.SetCount(type.Id, wanted[type.Id]) < wanted[type.Id]);
        if (missing > 0)
        {
            notes.Add("Some markers didn't fit in this save's marker inventory.");
        }
    }

    private void ApplyRapSheet(SaveFile save, List<string> notes)
    {
        if (!save.Stats.IsAvailable)
        {
            notes.Add("The Rap Sheet was skipped because this save version doesn't support it.");
            return;
        }
        if (_root["rapSheet"] is not { } sheet)
        {
            notes.Add("The file has no Rap Sheet data.");
            return;
        }
        ApplyStats(save, Need(sheet, "pursuit"), RapSheet.PursuitFields);
        ApplyStats(save, Need(sheet, "costToState"), RapSheet.DamageFields);
    }

    private static void ApplyStats(SaveFile save, JsonNode node, IEnumerable<StatField> fields)
    {
        foreach (var field in fields)
        {
            var value = Long(node, SaveExport.Key(field.Name));
            save.Stats.Set(field, (int)Math.Clamp(field.IsTime ? value * RapSheet.TicksPerSecond : value, 0, int.MaxValue));
        }
    }

    private void ApplyOptions(SaveFile save, List<string> notes)
    {
        var node = Need(_root, "options");
        var options = save.Options;
        options.AutoSave = Bool(node, "autoSave");
        options.Rearview = Bool(node, "rearview");
        options.Damage = Bool(node, "damage");
        options.JumpCam = Bool(node, "jumpCam");
        options.Kilometers = Str(node, "speedUnits") == "Metric";
        options.InteractiveMusic = Bool(node, "interactiveMusic");
        options.EaTrax = Bool(node, "eaTrax");
        SetChoice(GameOptions.MapModes, Str(node, "raceMap"), v => options.RaceMap = v);
        SetChoice(GameOptions.MapModes, Str(node, "freeRoamMap"), v => options.FreeRoamMap = v);
        if (!SetChoice(options.AudioModes, Str(node, "audioMode"), v => options.AudioMode = v))
        {
            notes.Add("The audio mode isn't available on this platform, so it was kept.");
        }
        if (options.HasWidescreen && node["widescreen"] is not null)
        {
            options.Widescreen = Bool(node, "widescreen");
        }
        var volumes = Need(node, "volumes");
        foreach (var volume in GameOptions.Volumes)
        {
            options.SetVolume(volume, Int(volumes, SaveExport.Key(volume.Name)));
        }
        var from = Need(node, "player");
        var player = options.Player(0);
        player.Gauges = Bool(from, "gauges");
        player.RaceInformation = Bool(from, "raceInformation");
        player.Score = Bool(from, "score");
        player.Leaderboard = Bool(from, "leaderboard");
        player.SplitTime = Bool(from, "splitTime");
        player.ManualTransmission = Bool(from, "manualTransmission");
        SetChoice(GameOptions.Cameras, Str(from, "camera"), v => player.Camera = v);
    }

    private static bool SetChoice(IReadOnlyList<string> names, string label, Action<int> apply)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (names[i] == label)
            {
                apply(i);
                return true;
            }
        }
        return false;
    }

    private static ImportedCar ParseCar(JsonNode node)
    {
        var upgrades = Items(node["upgrades"]).Select(u => new ImportedUpgrade(Int(u, "level"), Bool(u, "junkman"))).ToList();
        var parts = Items(node["parts"]).Select(ParsePart).ToList();
        return new ImportedCar(
            Str(node, "name"),
            Hex(Str(node, "vehicleKey")),
            Hex(Str(node, "frontendKey")),
            Hex(Str(node, "filter")),
            Bool(node, "isCurrent"),
            node["police"] is { } police ? ParsePolice(police) : null,
            upgrades,
            parts);
    }

    private static ImportedPart ParsePart(JsonNode node)
    {
        var slot = Int(node, "slot");
        if (slot < 0 || slot >= Layout.PartSlots)
        {
            throw new FormatException("slot");
        }
        var part = (ushort)Math.Clamp(Int(node, "part"), 0, ushort.MaxValue);
        return new ImportedPart(slot, part, Value(node["nameHash"]) is { } hash ? Hex(hash) : null);
    }

    private static ImportedPolice ParsePolice(JsonNode node)
    {
        var impound = Enum.TryParse<ImpoundState>(Str(node, "impound"), out var state) && Enum.IsDefined(state) ? state : ImpoundState.None;
        var heat = float.Parse(Str(node, "heat"), NumberStyles.Float, CultureInfo.InvariantCulture);
        return new ImportedPolice(
            float.IsFinite(heat) ? heat : 0f,
            UInt(node, "bounty"),
            Int(node, "evaded"),
            Int(node, "busted"),
            Int(node, "strikes"),
            Int(node, "maxStrikes"),
            impound,
            Int(node, "impoundDaysLeft"),
            Counts(node, "unserved"),
            Counts(node, "served"));
    }

    private static ImportedBin ParseBin(JsonNode node) => new(
        Int(node, "rank"),
        Int(node, "challenges"),
        Int(node, "raceWins"),
        Items(node["milestones"]).Select(ParseState).ToList(),
        Items(node["traps"]).Select(ParseState).ToList());

    private static ProgressState ParseState(JsonNode node)
    {
        var recorded = float.Parse(Str(node, "recorded"), NumberStyles.Float, CultureInfo.InvariantCulture);
        return new ProgressState(Int(node, "state"), float.IsFinite(recorded) ? recorded : 0f);
    }

    private static int[] Counts(JsonNode node, string key) => Items(node[key]).Select(item => int.Parse(Value(item) ?? "0", CultureInfo.InvariantCulture)).ToArray();

    private static JsonNode ParseXml(string text)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var source = new StringReader(text);
        using var reader = XmlReader.Create(source, settings);
        var root = XDocument.Load(reader).Root ?? throw new FormatException("empty");
        return FromElement(root, false) ?? throw new FormatException("empty");
    }

    private static JsonNode? FromElement(XElement element, bool isItem)
    {
        if (!isItem && ListNames.Contains(element.Name.LocalName))
        {
            var list = new JsonArray();
            foreach (var child in element.Elements())
            {
                list.Add(FromElement(child, true));
            }
            return list;
        }
        if (!element.HasElements)
        {
            return JsonValue.Create(element.Value);
        }
        var values = new JsonObject();
        foreach (var child in element.Elements())
        {
            values[child.Name.LocalName] = FromElement(child, false);
        }
        return values;
    }

    private static IEnumerable<JsonNode> Items(JsonNode? node) => node is JsonArray list ? list.Where(item => item is not null).Select(item => item!) : [];

    private static JsonNode Need(JsonNode node, string key) => node[key] ?? throw new FormatException(key);

    private static string? Value(JsonNode? node) => node is JsonValue value
        ? value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString()
        : null;

    private static string Str(JsonNode node, string key) => Value(node[key]) ?? throw new FormatException(key);

    private static int Int(JsonNode node, string key) => int.Parse(Str(node, key), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

    private static uint UInt(JsonNode node, string key) => uint.Parse(Str(node, key), NumberStyles.None, CultureInfo.InvariantCulture);

    private static long Long(JsonNode node, string key) => long.Parse(Str(node, key), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

    private static bool Bool(JsonNode node, string key) => bool.Parse(Str(node, key));

    private static uint Hex(string text) => uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
