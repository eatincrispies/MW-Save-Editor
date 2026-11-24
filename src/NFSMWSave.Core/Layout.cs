namespace NFSMWSave.Core;

public static class Layout
{
    public const uint Magic = 0x4D433032;
    public const int HeaderSignedSize = 0x18;
    public const int FileLengthOffset = 0x04;
    public const int InfoSizeOffset = 0x08;
    public const int ProfileSizeOffset = 0x0C;
    public const int InfoSignatureOffset = 0x10;
    public const int ProfileSignatureOffset = 0x14;
    public const int HeaderSignatureOffset = 0x18;
    public const int InfoOffset = 0x1C;
    public const int InfoSize = 8;
    public const int ProfileOffset = 0x24;
    public const int ProfileHashedOffset = 0x34;
    public const int HashSize = 16;

    public const int GameplayOffset = 0x34;
    public const int GameplaySize = 0x4000;
    public const uint GameplayMagic = 0x656D6147;

    public const int CurrentCar = 0x4034;
    public const int CurrentBin = 0x4038;
    public const int Cash = 0x4039;
    public const int SpecialFlags = 0x403F;
    public const int CaseFileName = 0x429D;
    public const int CaseFileNameSize = 16;
    public const int RaceCount = 0x42AD;
    public const int Races = 0x42B1;
    public const int RaceSize = 16;
    public const int MaxRaces = 300;
    public const int Unlocks = 0x5571;
    public const int UnlockCount = 57;
    public const int UnlockSize = 8;
    public const int Markers = 0x5739;
    public const int MarkerCount = 63;
    public const int MarkerSize = 12;
    public const int StableStamp = 0x5A2D;
    public const int ProfileName = 0x5A31;
    public const int ProfileNameSize = 32;
    public const int Options = 0x5B41;

    public const int CarCount = 200;
    public const int CarSize = 0x14;
    public const int CustomizationTable = 0xFA0;
    public const int CustomizationCount = 75;
    public const int CustomizationSize = 0x198;
    public const int CustomizationHandle = 0x194;
    public const int CustomizationPreset = 0x190;
    public const int PartSlots = 139;
    public const int CareerCount = 25;
    public const int CareerSize = 0x38;
    public const uint InvalidHandle = 0xFFFFFFFF;
    public const byte InvalidIndex = 0xFF;
    public const uint FilterCareer = 2;
    public const uint FilterStock = 1;
    public const uint FilterNewCareerCar = 0xF0002;
    public const int QuickRaceSlots = 11;
    public const uint DefaultCarHandle = 0x12345678;
}

public enum SavePlatform
{
    PC,
    Xbox360,
    GameCube,
    PS2,
    OriginalXbox,
}

public sealed record SaveLayout(
    SavePlatform Platform,
    string PlatformName,
    bool BigEndian,
    int ProfileSize,
    int CarDb,
    int CompletedOnce,
    int CompletedSize,
    int HighScores,
    int AutoSave,
    int BoolSize,
    int SpeedUnits,
    int JumpCam,
    int Audio)
{
    public const int OriginalXboxProfileSize = 0xF4B4;
    private const uint GameCubeStoredLength = 0x12000;

    public static readonly SaveLayout Pc = new(SavePlatform.PC, "PC", false, 0xF848, 0x5BC5, 0xE88D, 1, 0xE88E, 0x14, 1, 0x17, 0x23, 0x28) { QuickRaceFromEnd = 0x406 };
    public static readonly SaveLayout Xbox360 = new(SavePlatform.Xbox360, "Xbox 360", true, 0xF4BC, 0x5BCD, 0xE895, 1, 0xE896, 0x1C, 1, 0x1F, 0x2B, 0x30) { QuickRaceFromEnd = 0x72 };
    public static readonly SaveLayout OriginalXbox = new(SavePlatform.OriginalXbox, "Original Xbox", false, OriginalXboxProfileSize, 0x5BC5, 0xE88D, 1, 0xE88E, 0x14, 1, 0x17, 0x23, 0x28) { QuickRaceFromEnd = 0x72 };
    public static readonly SaveLayout GameCube = new(SavePlatform.GameCube, "GameCube", true, 0xF4BD, 0x5C01, 0xE8C9, 4, 0xE8CD, 0x14, 4, 0x20, 0x2C, 0x34);
    public static readonly SaveLayout Ps2 = new(SavePlatform.PS2, "PS2", false, 0xF4BD, 0x5C01, 0xE8C9, 4, 0xE8CD, 0x14, 4, 0x20, 0x2C, 0x34);
    public static readonly SaveLayout Ps2Early = new(SavePlatform.PS2, "PS2 Early Build", false, 0xD91E, 0x5B7B, 0xCD13, 4, 0xCD17, 0x14, 4, 0x20, 0x2C, 0x34)
    {
        SectionShift = -0x66,
        OptionsBase = 0x5ABB,
        CustomizationCount = 60,
        CareerCount = 10,
        MinGameplayVersion = 7,
        TrapSize = 16,
        TrapFlags = 4,
        TrapBin = 6,
        TrapRequired = 8,
        TrapRecorded = 12,
        CanAddCars = false,
        HasEndgameFlags = false,
        HasConfirmedStats = false,
        Playlist = 0x59EB,
        HasPlayMode = false,
    };

    public int SectionShift { get; init; }
    public int QuickRaceFromEnd { get; init; } = 0x3C;
    public int OptionsBase { get; init; } = Layout.Options;
    public int CustomizationCount { get; init; } = Layout.CustomizationCount;
    public int CareerCount { get; init; } = Layout.CareerCount;
    public uint MinGameplayVersion { get; init; } = 8;
    public int TrapSize { get; init; } = 20;
    public int TrapFlags { get; init; }
    public int TrapBin { get; init; } = 2;
    public int TrapRequired { get; init; } = 12;
    public int TrapRecorded { get; init; } = 16;
    public bool CanAddCars { get; init; } = true;
    public bool HasEndgameFlags { get; init; } = true;
    public bool HasConfirmedStats { get; init; } = true;
    public int Playlist { get; init; } = 0x5A51;
    public bool HasPlayMode { get; init; } = true;

    public int CaseFileName => Layout.CaseFileName + SectionShift;
    public int RaceCount => Layout.RaceCount + SectionShift;
    public int Races => Layout.Races + SectionShift;
    public int Unlocks => Layout.Unlocks + SectionShift;
    public int Markers => Layout.Markers + SectionShift;
    public int StableStamp => Layout.StableStamp + SectionShift;
    public int ProfileName => Layout.ProfileName + SectionShift;
    public int CareerTable => Layout.CustomizationTable + CustomizationCount * Layout.CustomizationSize;
    public int SoldBounty => CareerTable + CareerCount * Layout.CareerSize;

    public int FileSize => Layout.ProfileOffset + ProfileSize;
    public int QuickRaceCars => FileSize - QuickRaceFromEnd;
    public uint StoredFileLength => Platform == SavePlatform.GameCube ? GameCubeStoredLength : (uint)FileSize;
    public int ProfileHashOffset => FileSize - Layout.HashSize;
    public bool CanRenameAlias => Platform != SavePlatform.OriginalXbox;
    public bool UppercaseAlias =>Platform is SavePlatform.Xbox360 or SavePlatform.GameCube;

    public static SaveLayout Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length < Layout.ProfileOffset)
        {
            throw new SaveFormatException("This is not a Most Wanted save.");
        }
        var big = data[0] == (byte)'M' && data[1] == (byte)'C' && data[2] == (byte)'0' && data[3] == (byte)'2';
        var little = data[0] == (byte)'2' && data[1] == (byte)'0' && data[2] == (byte)'C' && data[3] == (byte)'M';
        if (!big && !little)
        {
            throw new SaveFormatException("This is not a Most Wanted save.");
        }
        var size = big
            ? System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data[Layout.ProfileSizeOffset..])
            : System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(data[Layout.ProfileSizeOffset..]);
        return (big, size) switch
        {
            (false, 0xF848) => Pc,
            (true, 0xF4BC) => Xbox360,
            (true, 0xF4BD) => GameCube,
            (false, 0xF4BD) => Ps2,
            (false, 0xD91E) => Ps2Early,
            (false, OriginalXboxProfileSize) => OriginalXbox,
            _ => throw new SaveFormatException("This save has an unsupported layout."),
        };
    }
}
