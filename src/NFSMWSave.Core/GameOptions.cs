namespace NFSMWSave.Core;

public sealed record VolumeField(string Name, int Index);

public sealed record TraxSong(string Title, string Artist);

public sealed record PlaylistEntry(int Song, int Play);

public sealed class PlayerOptions
{
    private const int GaugesBool = 0;
    private const int PositionBool = 1;
    private const int ScoreBool = 3;
    private const int VibrationBool = 4;
    private const int LeaderboardBool = 5;
    public const int ControlTypes = 5;
    private const int BoolCount = 8;
    private const int CameraAfterBools = 4;
    private const int SplitAfterBools = 8;
    private const int TransmissionAfterBools = 9;
    public const int SplitOn = 0;
    public const int SplitOff = 4;

    private readonly SaveBuffer _d;
    private readonly int _at;
    private readonly int _boolSize;

    internal PlayerOptions(SaveBuffer d, int at, int boolSize)
    {
        _d = d;
        _at = at;
        _boolSize = boolSize;
    }

    public bool Gauges
    {
        get => _d.Bool(_at + GaugesBool * _boolSize, _boolSize);
        set => _d.Bool(_at + GaugesBool * _boolSize, _boolSize, value);
    }

    public bool RaceInformation
    {
        get => _d.Bool(_at + PositionBool * _boolSize, _boolSize);
        set => _d.Bool(_at + PositionBool * _boolSize, _boolSize, value);
    }

    public bool Score
    {
        get => _d.Bool(_at + ScoreBool * _boolSize, _boolSize);
        set => _d.Bool(_at + ScoreBool * _boolSize, _boolSize, value);
    }

    public bool Leaderboard
    {
        get => _d.Bool(_at + LeaderboardBool * _boolSize, _boolSize);
        set => _d.Bool(_at + LeaderboardBool * _boolSize, _boolSize, value);
    }

    public bool Vibration
    {
        get => _d.Bool(_at + VibrationBool * _boolSize, _boolSize);
        set => _d.Bool(_at + VibrationBool * _boolSize, _boolSize, value);
    }

    public int ControlType
    {
        get => _d.I32(_at + BoolCount * _boolSize);
        set => _d.I32(_at + BoolCount * _boolSize, Math.Clamp(value, 0, ControlTypes - 1));
    }

    public int Camera
    {
        get => _d.I32(_at + BoolCount * _boolSize + CameraAfterBools);
        set => _d.I32(_at + BoolCount * _boolSize + CameraAfterBools, value);
    }

    public bool SplitTime
    {
        get => _d[_at + BoolCount * _boolSize + SplitAfterBools] != SplitOff;
        set => _d[_at + BoolCount * _boolSize + SplitAfterBools] = (byte)(value ? SplitOn : SplitOff);
    }

    public bool ManualTransmission
    {
        get => _d[_at + BoolCount * _boolSize + TransmissionAfterBools] == 1;
        set => _d[_at + BoolCount * _boolSize + TransmissionAfterBools] = (byte)(value ? 1 : 0);
    }
}

public sealed class GameOptions(SaveBuffer d)
{
    private const int WidescreenOffset = 0x10;
    private const int WidescreenBoolSize = 4;
    private const int RaceMapAfterUnits = 1;
    private const int FreeRoamMapAfterUnits = 2;
    private const int AudioModeOffset = 0x24;
    private const int InteractiveOffset = 0x28;
    private const int EaTraxOffset = 0x2C;
    private const int PlayModeOffset = 0x30;
    private const int AudioSize = 0x34;
    private const int PlaylistEntrySize = 8;
    private const int PlaylistPlay = 4;

    public static readonly IReadOnlyList<string> PlayModes = ["Sequential", "Random"];
    public static readonly IReadOnlyList<string> SongPlay = ["Off", "Menu", "Racing", "All"];

    public static readonly IReadOnlyList<TraxSong> Songs =
    [
        new("Nine Thou (Superstars Remix)", "Styles Of Beyond"),
        new("Do Ya Thang", "T.I. Presents The P$C"),
        new("I Am Rock", "Rock"),
        new("In A Hood Near You", "Suni Clay"),
        new("Let's Move", "The Perceptionists"),
        new("Sets Go Up", "Juvenile"),
        new("Fired Up", "Hush"),
        new("B-Side Wins Again feat. Chuck D", "DJ Spooky and Dave Lombardo"),
        new("Shapeshifter", "Celldweller feat. Styles Of Beyond"),
        new("Tilted", "Lupe Fiasco"),
        new("Feed The Addiction", "Ils"),
        new("One Good Reason", "Celldweller"),
        new("We Control", "Hyper"),
        new("Skinnyman", "Static-X"),
        new("Barrier Break", "Diesel Boy + Kaos"),
        new("Decadence", "Disturbed"),
        new("You'll Be Under My Wheels", "The Prodigy"),
        new("Tao Of The Machine (Scott Humphrey's Remix)", "The Roots and BT"),
        new("You Must Follow (Evol Intent VIP)", "Stratus"),
        new("Blood And Thunder", "Mastodon"),
        new("Broken Sword", "Evol Intent, Mayhem & Thinktank"),
        new("Hand Of Blood", "Bullet For My Valentine"),
        new("The Mann", "Paul Linford and Chris Vrenna"),
        new("Blinded In Chains", "Avenged Sevenfold"),
        new("Feels Just Like It Should (Timo Maas Remix)", "Jamiroquai"),
        new("Most Wanted Mash Up", "Paul Linford and Chris Vrenna"),
    ];
    private const int PlayerBoolCount = 8;
    private const int PlayerTailSize = 0xC;

    public static readonly IReadOnlyList<VolumeField> Volumes =
    [
        new("Master", 0),
        new("Speech", 1),
        new("Menu music", 2),
        new("Race music", 3),
        new("Sound effects", 4),
        new("Engine", 5),
        new("Car", 6),
        new("Ambient", 7),
        new("Speed", 8),
    ];

    public static readonly IReadOnlyList<VolumeField> MenuVolumes =
    [
        new("Sound Effects Volume", 4),
        new("Car Volume", 6),
        new("Speech Volume", 1),
        new("Menu Music Volume", 2),
        new("Game Music Volume", 3),
    ];

    public static readonly IReadOnlyList<string> MapModes = ["Static", "Rotate", "Off"];
    public static readonly IReadOnlyList<string> Cameras = ["Bumper", "Hood", "Close", "Far", "Super Far"];

    private SaveLayout Map => d.Map;

    public IReadOnlyList<string> AudioModes => Map.BoolSize == WidescreenBoolSize ? ["Mono", "Stereo", "Dolby™ Pro Logic II"] : ["Mono", "Stereo"];

    public bool HasWidescreen => Map.BoolSize == WidescreenBoolSize;

    public bool Widescreen
    {
        get => HasWidescreen && d.Bool(Map.OptionsBase + WidescreenOffset, Map.BoolSize);
        set
        {
            if (HasWidescreen)
            {
                d.Bool(Map.OptionsBase + WidescreenOffset, Map.BoolSize, value);
            }
        }
    }

    public bool AutoSave
    {
        get => d.Bool(Map.OptionsBase + Map.AutoSave, Map.BoolSize);
        set => d.Bool(Map.OptionsBase + Map.AutoSave, Map.BoolSize, value);
    }

    public bool Rearview
    {
        get => d.Bool(Map.OptionsBase + Map.AutoSave + Map.BoolSize, Map.BoolSize);
        set => d.Bool(Map.OptionsBase + Map.AutoSave + Map.BoolSize, Map.BoolSize, value);
    }

    public bool Damage
    {
        get => d.Bool(Map.OptionsBase + Map.AutoSave + 2 * Map.BoolSize, Map.BoolSize);
        set => d.Bool(Map.OptionsBase + Map.AutoSave + 2 * Map.BoolSize, Map.BoolSize, value);
    }

    public bool JumpCam
    {
        get => d.Bool(Map.OptionsBase + Map.JumpCam, Map.BoolSize);
        set => d.Bool(Map.OptionsBase + Map.JumpCam, Map.BoolSize, value);
    }

    public bool Kilometers
    {
        get => d[Map.OptionsBase + Map.SpeedUnits] == 1;
        set => d[Map.OptionsBase + Map.SpeedUnits] = (byte)(value ? 1 : 0);
    }

    public int RaceMap
    {
        get => d[Map.OptionsBase + Map.SpeedUnits + RaceMapAfterUnits];
        set => d[Map.OptionsBase + Map.SpeedUnits + RaceMapAfterUnits] = (byte)value;
    }

    public int FreeRoamMap
    {
        get => d[Map.OptionsBase + Map.SpeedUnits + FreeRoamMapAfterUnits];
        set => d[Map.OptionsBase + Map.SpeedUnits + FreeRoamMapAfterUnits] = (byte)value;
    }

    public int AudioMode
    {
        get => d.I32(Map.OptionsBase + Map.Audio + AudioModeOffset);
        set => d.I32(Map.OptionsBase + Map.Audio + AudioModeOffset, value);
    }

    public bool InteractiveMusic
    {
        get => d.I32(Map.OptionsBase + Map.Audio + InteractiveOffset) != 0;
        set => d.I32(Map.OptionsBase + Map.Audio + InteractiveOffset, value ? 1 : 0);
    }

    public bool EaTrax
    {
        get => d.I32(Map.OptionsBase + Map.Audio + EaTraxOffset) != 0;
        set => d.I32(Map.OptionsBase + Map.Audio + EaTraxOffset, value ? 1 : 0);
    }

    public bool HasPlayMode => Map.HasPlayMode;

    public int PlayMode
    {
        get => HasPlayMode ? d.I32(Map.OptionsBase + Map.Audio + PlayModeOffset) : 0;
        set
        {
            if (HasPlayMode)
            {
                d.I32(Map.OptionsBase + Map.Audio + PlayModeOffset, Math.Clamp(value, 0, PlayModes.Count - 1));
            }
        }
    }

    public IReadOnlyList<PlaylistEntry> Playlist => Enumerable.Range(0, Songs.Count)
        .Select(i => new PlaylistEntry((int)d.U32(Map.Playlist + i * PlaylistEntrySize), d[Map.Playlist + i * PlaylistEntrySize + PlaylistPlay]))
        .ToList();

    public bool HasPlaylist
    {
        get
        {
            var entries = Playlist;
            return entries.Select(e => e.Song).Distinct().Count() == Songs.Count && entries.All(e => e.Song >= 0 && e.Song < Songs.Count && e.Play < SongPlay.Count);
        }
    }

    public void SetPlaylist(IReadOnlyList<PlaylistEntry> entries)
    {
        if (entries.Count != Songs.Count || entries.Select(e => e.Song).Distinct().Count() != Songs.Count || entries.Any(e => e.Song < 0 || e.Song >= Songs.Count))
        {
            throw new ArgumentException("The playlist must hold every song once.");
        }
        for (var i = 0; i < entries.Count; i++)
        {
            d.U32(Map.Playlist + i * PlaylistEntrySize, (uint)entries[i].Song);
            d[Map.Playlist + i * PlaylistEntrySize + PlaylistPlay] = (byte)Math.Clamp(entries[i].Play, 0, SongPlay.Count - 1);
        }
    }

    public PlayerOptions Player(int index)
    {
        var size = PlayerBoolCount * Map.BoolSize + PlayerTailSize;
        return new PlayerOptions(d, Map.OptionsBase + Map.Audio + AudioSize + index * size, Map.BoolSize);
    }

    public int GetVolume(VolumeField field) => (int)MathF.Round(d.F32(Map.OptionsBase + Map.Audio + field.Index * 4) * 100f);

    public void SetVolume(VolumeField field, int percent) => d.F32(Map.OptionsBase + Map.Audio + field.Index * 4, Math.Clamp(percent, 0, 100) / 100f);
}
