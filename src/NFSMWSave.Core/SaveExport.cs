using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace NFSMWSave.Core;

public enum ExportFormat
{
    Json,
    Xml,
}

public static class SaveExport
{
    internal const string FormatName = "NFSMW Save Export";
    internal const int FormatVersion = 2;

    private static readonly string[] UpgradeNames = ["Tires", "Brakes", "Suspension", "Transmission", "Engine", "Turbo", "Nitrous"];
    private static readonly Dictionary<int, string> SlotNames = BuildSlotNames();
    private static readonly JsonSerializerOptions JsonStyle = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static IReadOnlyDictionary<int, string> PartSlotNames => SlotNames;

    public static string ToJson(SaveFile save) => Build(save).ToJsonString(JsonStyle);

    public static string ToXml(SaveFile save)
    {
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), ToElement("nfsmwSave", Build(save)));
        return $"{document.Declaration}{Environment.NewLine}{document}";
    }

    public static void Write(SaveFile save, ExportFormat format, string path)
    {
        var text = format == ExportFormat.Json ? ToJson(save) : ToXml(save);
        var temp = path + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));
        File.Move(temp, path, true);
    }

    private static JsonObject Build(SaveFile save)
    {
        var hashes = save.Stable.PresetHashes();
        var root = new JsonObject
        {
            ["format"] = FormatName,
            ["formatVersion"] = FormatVersion,
            ["exportedUtc"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["source"] = new JsonObject
            {
                ["platform"] = save.PlatformName,
                ["container"] = save.FormatName,
            },
            ["career"] = CareerNode(save),
        };
        var completion = CompletionStats.Compute(save);
        if (completion is not null)
        {
            root["completion"] = new JsonObject
            {
                ["game"] = completion.Overall,
                ["career"] = completion.Career,
                ["rapSheet"] = completion.RapSheet,
                ["challengeSeries"] = completion.Challenge,
                ["challengesDone"] = completion.ChallengeDone,
                ["challengesTotal"] = completion.ChallengeTotal,
            };
        }
        if (save.Progress.IsReadable)
        {
            root["blacklist"] = ListOf(GameData.Rivals.Select(r => RivalNode(save, r)));
        }
        root["careerCars"] = ListOf(save.Stable.Cars.Select(c => CarNode(save, c, hashes)));
        root["myCars"] = ListOf(save.Stable.MyCars.Select(c => CarNode(save, c, hashes)));
        var history = save.Stable.ReadHistory();
        root["soldHistory"] = new JsonObject
        {
            ["bounty"] = history.Bounty,
            ["evaded"] = history.Evaded,
            ["busted"] = history.Busted,
            ["unserved"] = CountList(history.Unserved),
            ["served"] = CountList(history.Served),
        };
        root["races"] = ListOf(save.Races.Races.Select(RaceNode));
        root["markers"] = ListOf(MarkerInventory.Types.Select(t => (JsonNode)new JsonObject
        {
            ["id"] = t.Id,
            ["name"] = t.Name,
            ["count"] = save.Markers.Count(t.Id),
        }));
        root["unlocks"] = ListOf(save.Unlocks.Levels.Select(u => (JsonNode)new JsonObject
        {
            ["entity"] = u.Entity,
            ["level"] = u.Level,
            ["maxLevel"] = u.MaxLevel,
        }));
        if (save.Stats.IsAvailable)
        {
            root["rapSheet"] = new JsonObject
            {
                ["pursuit"] = StatsNode(save, RapSheet.PursuitFields),
                ["costToState"] = StatsNode(save, RapSheet.DamageFields),
            };
        }
        root["options"] = OptionsNode(save);
        return root;
    }

    private static JsonObject CareerNode(SaveFile save)
    {
        var career = save.Career;
        var rival = GameData.Rivals.FirstOrDefault(r => r.Rank == career.Rank);
        var current = save.Stable.Cars.Concat(save.Stable.MyCars).FirstOrDefault(c => c.Handle == career.CurrentCar);
        var flags = new JsonObject();
        foreach (var flag in Enum.GetValues<CareerFlags>())
        {
            flags[Key(flag.ToString())] = career.GetFlag(flag);
        }
        flags["completedOnce"] = career.CompletedOnce;
        var node = new JsonObject
        {
            ["alias"] = career.ProfileName,
            ["caseFile"] = career.CaseFile,
            ["rank"] = career.Rank,
            ["rankName"] = rival is null ? "Prologue" : $"#{rival.Rank} {rival.Name}",
            ["cash"] = career.Cash,
            ["totalBounty"] = save.Stable.TotalBounty,
            ["pursuitsEvaded"] = save.Stable.TotalEvaded,
            ["timesBusted"] = save.Stable.TotalBusted,
            ["infractions"] = save.Stable.TotalInfractions,
            ["flags"] = flags,
        };
        if (current is not null)
        {
            node["currentCar"] = current.Name;
            node["currentCarVehicleKey"] = Hex(current.VehicleKey);
        }
        return node;
    }

    private static JsonNode RivalNode(SaveFile save, RivalInfo rival)
    {
        var bin = save.Progress.GetBin(rival.Rank);
        var (milestones, traps) = save.Progress.BinStates(rival.Rank);
        return new JsonObject
        {
            ["rank"] = rival.Rank,
            ["name"] = rival.Name,
            ["car"] = rival.Car,
            ["challenges"] = bin.Challenges,
            ["raceWins"] = bin.RaceWins,
            ["milestonesDone"] = bin.MilestonesDone,
            ["milestonesTotal"] = bin.MilestonesTotal,
            ["milestones"] = ListOf(milestones.Select(StateNode)),
            ["traps"] = ListOf(traps.Select(StateNode)),
        };
    }

    private static JsonNode StateNode(ProgressState state) => new JsonObject
    {
        ["state"] = state.State,
        ["recorded"] = float.IsFinite(state.Recorded) ? state.Recorded : 0f,
    };

    private static JsonArray CountList(IEnumerable<int> counts) => ListOf(counts.Select(c => (JsonNode)JsonValue.Create(c)));

    private static JsonNode CarNode(SaveFile save, CareerCar car, Func<ushort, uint?> hashes)
    {
        var node = new JsonObject
        {
            ["name"] = car.Name,
            ["vehicleKey"] = Hex(car.VehicleKey),
            ["frontendKey"] = Hex(car.FeKey),
            ["filter"] = Hex(save.Stable.FilterOf(car)),
            ["isCurrent"] = car.Handle == save.Career.CurrentCar,
        };
        if (car.IsCareer)
        {
            node["police"] = new JsonObject
            {
                ["heat"] = float.IsFinite(car.Heat) ? car.Heat : 0f,
                ["bounty"] = car.Bounty,
                ["evaded"] = (int)car.Evaded,
                ["busted"] = (int)car.Busted,
                ["strikes"] = car.Strikes,
                ["maxStrikes"] = car.MaxStrikes,
                ["impound"] = car.Impound.ToString(),
                ["impoundDaysLeft"] = car.ImpoundDaysLeft,
                ["unservedInfractions"] = car.UnservedInfractions,
                ["totalInfractions"] = car.AllInfractions,
                ["unserved"] = CountList(car.UnservedCounts),
                ["served"] = CountList(car.ServedCounts),
            };
        }
        if (car.HasCustomization)
        {
            node["upgrades"] = ListOf(Enumerable.Range(0, CareerCar.PerformanceSlots).Select(slot => (JsonNode)new JsonObject
            {
                ["name"] = UpgradeNames[slot],
                ["level"] = car.GetLevel(slot),
                ["junkman"] = car.GetJunkman(slot),
            }));
            node["tuning"] = new JsonObject
            {
                ["active"] = car.ActiveTuning,
                ["values"] = ListOf(car.RawTuning.Select(v => (JsonNode)JsonValue.Create(v))),
            };
            node["parts"] = ListOf(Enumerable.Range(0, Layout.PartSlots)
                .Where(slot => car.GetPart(slot) != CarPaints.NoPart)
                .Select(slot => PartNode(save, car, slot, hashes)));
        }
        return node;
    }

    private static JsonNode PartNode(SaveFile save, CareerCar car, int slot, Func<ushort, uint?> hashes)
    {
        var part = car.GetPart(slot);
        var node = new JsonObject { ["slot"] = slot };
        if (SlotNames.TryGetValue(slot, out var slotName))
        {
            node["slotName"] = slotName;
        }
        node["part"] = (int)part;
        if (hashes(part) is { } hash)
        {
            node["nameHash"] = Hex(hash);
        }
        if (CarParts.Kinds.Any(k => k.Slot == slot) && save.Stable.PartsFor(car, slot).FirstOrDefault(p => p.Part == part) is { } choice)
        {
            node["partName"] = choice.Name;
        }
        return node;
    }

    private static JsonNode RaceNode(RaceRecord race) => new JsonObject
    {
        ["id"] = race.Id,
        ["group"] = Career.RaceGroupName(race.Group),
        ["careerUnlocked"] = race.Has(RaceFlags.CareerUnlocked),
        ["careerCompleted"] = race.Has(RaceFlags.CareerCompleted),
        ["quickRaceUnlocked"] = race.Has(RaceFlags.QuickRaceUnlocked),
        ["quickRaceCompleted"] = race.Has(RaceFlags.QuickRaceCompleted),
        ["onlineUnlocked"] = race.Has(RaceFlags.OnlineUnlocked),
    };

    private static JsonObject StatsNode(SaveFile save, IEnumerable<StatField> fields)
    {
        var node = new JsonObject();
        foreach (var field in fields)
        {
            node[Key(field.Name)] = field.IsTime ? save.Stats.Get(field) / RapSheet.TicksPerSecond : save.Stats.Get(field);
        }
        return node;
    }

    private static JsonObject OptionsNode(SaveFile save)
    {
        var options = save.Options;
        var volumes = new JsonObject();
        foreach (var volume in GameOptions.Volumes)
        {
            volumes[Key(volume.Name)] = options.GetVolume(volume);
        }
        var player = options.Player(0);
        var node = new JsonObject
        {
            ["autoSave"] = options.AutoSave,
            ["rearview"] = options.Rearview,
            ["damage"] = options.Damage,
            ["jumpCam"] = options.JumpCam,
            ["speedUnits"] = options.Kilometers ? "Metric" : "Imperial",
            ["raceMap"] = Label(GameOptions.MapModes, options.RaceMap),
            ["freeRoamMap"] = Label(GameOptions.MapModes, options.FreeRoamMap),
            ["audioMode"] = Label(options.AudioModes, options.AudioMode),
            ["interactiveMusic"] = options.InteractiveMusic,
            ["eaTrax"] = options.EaTrax,
            ["volumes"] = volumes,
            ["player"] = new JsonObject
            {
                ["gauges"] = player.Gauges,
                ["raceInformation"] = player.RaceInformation,
                ["score"] = player.Score,
                ["leaderboard"] = player.Leaderboard,
                ["camera"] = Label(GameOptions.Cameras, player.Camera),
                ["splitTime"] = player.SplitTime,
                ["manualTransmission"] = player.ManualTransmission,
            },
        };
        if (options.HasWidescreen)
        {
            node["widescreen"] = options.Widescreen;
        }
        return node;
    }

    private static Dictionary<int, string> BuildSlotNames()
    {
        var names = new Dictionary<int, string>
        {
            [CarParts.BodySlot] = "Body Kit",
            [CarParts.SpoilerSlot] = "Spoiler",
            [CarParts.RoofSlot] = "Roof Scoop",
            [CarParts.HoodSlot] = "Hood",
            [CarParts.RimSlot] = "Rims",
            [CarParts.RearRimSlot] = "Rear Rims",
            [CarPaints.PaintSlot] = "Paint",
            [CarPaints.VinylSlot] = "Vinyl",
            [CarPaints.RimPaintSlot] = "Rim Paint",
            [CarParts.WindowTintSlot] = "Window Tint",
            [CarParts.GaugeSlot] = "Custom Gauges",
            [CarParts.GaugeFaceSlot] = "Gauge Face",
            [CarParts.GaugeNeedleSlot] = "Gauge Needle",
            [CarParts.GaugeLetteringSlot] = "Gauge Lettering",
        };
        for (var i = 0; i < CarPaints.VinylColourSlots; i++)
        {
            names[CarPaints.VinylColourSlot + i] = $"Vinyl Colour {i + 1}";
        }
        foreach (var place in CarParts.DecalPlaces)
        {
            for (var i = 0; i < place.SlotCount; i++)
            {
                names[place.FirstSlot + i] = place.SlotCount == 1 ? $"{place.Title} Decal" : $"{place.Title} Decal {i + 1}";
            }
        }
        names[CarParts.LeftDoorLeftNumber] = "Left Door Number 1";
        names[CarParts.LeftDoorRightNumber] = "Left Door Number 2";
        names[CarParts.RightDoorLeftNumber] = "Right Door Number 1";
        names[CarParts.RightDoorRightNumber] = "Right Door Number 2";
        return names;
    }

    private static JsonArray ListOf(IEnumerable<JsonNode> items)
    {
        var list = new JsonArray();
        foreach (var item in items)
        {
            list.Add(item);
        }
        return list;
    }

    private static XElement ToElement(string name, JsonNode? node) => node switch
    {
        JsonObject values => new XElement(name, values.Where(p => p.Value is not null).Select(p => ToElement(p.Key, p.Value))),
        JsonArray items => new XElement(name, items.Select(item => ToElement(ItemName(name), item))),
        JsonValue value => new XElement(name, ValueText(value)),
        _ => new XElement(name),
    };

    private static string ItemName(string list) => list == "blacklist" ? "rival" : list.TrimEnd('s');

    private static string ValueText(JsonValue value)
    {
        var text = value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString();
        return new string(text.Where(XmlConvert.IsXmlChar).ToArray());
    }

    private static string Label(IReadOnlyList<string> names, int index) =>
        index >= 0 && index < names.Count ? names[index] : index.ToString(CultureInfo.InvariantCulture);

    private static string Hex(uint value) => value.ToString("X8", CultureInfo.InvariantCulture);

    internal static string Key(string text)
    {
        var key = new StringBuilder();
        foreach (var raw in text.Split([' ', '/'], StringSplitOptions.RemoveEmptyEntries))
        {
            var word = new string(raw.Where(char.IsAsciiLetterOrDigit).ToArray());
            if (word.Length > 0)
            {
                key.Append(key.Length == 0 ? char.ToLowerInvariant(word[0]) : char.ToUpperInvariant(word[0]));
                key.Append(word.AsSpan(1));
            }
        }
        return key.ToString();
    }
}
