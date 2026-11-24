namespace NFSMWSave.Core;

public sealed record StatField(string Name, int Offset, bool IsTime = false);

public sealed class RapSheet(SaveBuffer d)
{
    public const int TicksPerSecond = 4000;
    private const int CareerDetails = 0xB44;
    private const int CostToState = 0xBB4;

    public static readonly IReadOnlyList<StatField> PursuitFields =
    [
        new("Total pursuit time", CareerDetails, true),
        new("Cops involved", CareerDetails + 4),
        new("Cops damaged", CareerDetails + 8),
        new("Cops destroyed", CareerDetails + 12),
        new("Spike strips dodged", CareerDetails + 16),
        new("Roadblocks dodged", CareerDetails + 20),
        new("Helicopters involved", CareerDetails + 24),
        new("Cost to state", CareerDetails + 28),
    ];

    public static readonly IReadOnlyList<StatField> DamageFields =
    [
        new("Roadblocks deployed", CostToState),
        new("Traffic cars hit", CostToState + 4),
        new("Spike strips deployed", CostToState + 8),
        new("Helicopter spike strips", CostToState + 12),
        new("Cop cars deployed", CostToState + 16),
        new("Support vehicles deployed", CostToState + 20),
        new("Property damage value", CostToState + 24),
        new("Properties damaged", CostToState + 28),
    ];

    public bool IsAvailable => d.Map.HasConfirmedStats;

    public int PursuitSeconds => IsAvailable ? d.I32(d.Map.HighScores + CareerDetails) / TicksPerSecond : 0;

    internal int CareerValue(int index) => IsAvailable ? d.I32(d.Map.HighScores + CareerDetails + index * 4) : 0;

    internal int Score(int offset) => IsAvailable ? d.I32(d.Map.HighScores + offset) : 0;

    internal uint ScoreKey(int offset) => IsAvailable ? d.U32(d.Map.HighScores + offset) : 0;

    internal string ScoreText(int offset, int length) => IsAvailable ? d.Text(d.Map.HighScores + offset, length) : "";

    public int Get(StatField field) => IsAvailable ? d.I32(d.Map.HighScores + field.Offset) : 0;

    public void Set(StatField field, int value)
    {
        if (IsAvailable)
        {
            d.I32(d.Map.HighScores + field.Offset, Math.Max(value, 0));
        }
    }
}
