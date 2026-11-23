namespace NFSMWSave.Core;

public sealed record VolumeField(string Name, int Index);

public sealed class PlayerOptions
{
    private const int GaugesBool = 0;
    private const int PositionBool = 1;
    private const int ScoreBool = 3;
    private const int LeaderboardBool = 5;
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
    private const int AudioSize = 0x34;
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

    public PlayerOptions Player(int index)
    {
        var size = PlayerBoolCount * Map.BoolSize + PlayerTailSize;
        return new PlayerOptions(d, Map.OptionsBase + Map.Audio + AudioSize + index * size, Map.BoolSize);
    }

    public int GetVolume(VolumeField field) => (int)MathF.Round(d.F32(Map.OptionsBase + Map.Audio + field.Index * 4) * 100f);

    public void SetVolume(VolumeField field, int percent) => d.F32(Map.OptionsBase + Map.Audio + field.Index * 4, Math.Clamp(percent, 0, 100) / 100f);
}
