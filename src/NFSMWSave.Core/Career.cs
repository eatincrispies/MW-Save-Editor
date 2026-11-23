namespace NFSMWSave.Core;

[Flags]
public enum CareerFlags : uint
{
    CareerStarted = 1 << 0,
    HasRapSheet = 1 << 4,
    IntroDone = 1 << 5,
    GameOver = 1 << 11,
    BeenBustedOnce = 1 << 12,
    BeatenCareer = 1 << 14,
    BeatenSpecialChallenge = 1 << 15,
    BeatenChallengeSeries = 1 << 16,
    CastrolGt = 1 << 18,
    AwardedEpicCar = 1 << 20,
}

public sealed class Career(SaveBuffer d)
{
    public const uint MaxCash = 999_999_999;
    public const int FirstRank = 1;
    public const int LastRank = 15;
    public const int PrologueRank = 16;

    public string ProfileName => d.Text(d.Map.ProfileName, Layout.ProfileNameSize);
    public string CaseFile => d.Text(d.Map.CaseFileName, Layout.CaseFileNameSize);

    public uint StableStamp
    {
        get => d.U32(d.Map.StableStamp);
        set => d.U32(d.Map.StableStamp, value);
    }

    public uint Cash
    {
        get => d.U32(Layout.Cash);
        set => d.U32(Layout.Cash, Math.Min(value, MaxCash));
    }

    public int Rank
    {
        get => d[Layout.CurrentBin];
        internal set => d[Layout.CurrentBin] = (byte)value;
    }

    public uint CurrentCar
    {
        get => d.U32(Layout.CurrentCar);
        set => d.U32(Layout.CurrentCar, value);
    }

    public CareerFlags Flags
    {
        get => (CareerFlags)d.U32(Layout.SpecialFlags);
        set => d.U32(Layout.SpecialFlags, (uint)value);
    }

    public bool CompletedOnce
    {
        get => d.Map.HasConfirmedStats && d.Bool(d.Map.CompletedOnce, d.Map.CompletedSize);
        set
        {
            if (d.Map.HasConfirmedStats)
            {
                d.Bool(d.Map.CompletedOnce, d.Map.CompletedSize, value);
            }
        }
    }

    public bool HasStarted => Flags.HasFlag(CareerFlags.CareerStarted);

    public static string RaceGroupName(int group) => group switch
    {
        >= FirstRank and <= LastRank => $"Blacklist #{group}",
        PrologueRank => "Prologue",
        19 => "Challenge Series",
        21 => "Collector's Edition",
        _ => "Other",
    };

    public bool GetFlag(CareerFlags flag) => Flags.HasFlag(flag);

    public void SetFlag(CareerFlags flag, bool on) => Flags = on ? Flags | flag : Flags & ~flag;
}
