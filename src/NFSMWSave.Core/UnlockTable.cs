namespace NFSMWSave.Core;

public sealed class UnlockTable(SaveBuffer d)
{
    private const int QuickRaceLevel = 4;

    private static readonly (int Entity, int Level)[] MaxLevels =
    [
        (4, 3), (5, 4), (6, 3), (7, 4), (8, 4), (9, 3), (10, 3),
        (11, 4), (12, 5), (13, 6), (14, 6), (15, 6), (17, 4), (18, 4), (23, 4), (40, 6),
        (43, 1), (44, 1), (46, 2), (48, 3),
    ];

    public int UnlockedCount => MaxLevels.Count(m => (sbyte)d[At(m.Entity)] >= m.Level && (sbyte)d[At(m.Entity) + QuickRaceLevel] >= m.Level);

    public int TotalCount => MaxLevels.Length;

    public IEnumerable<(int Entity, int Level, int MaxLevel)> Levels => MaxLevels.Select(m => (m.Entity, (int)(sbyte)d[At(m.Entity)], m.Level));

    internal void SetLevel(int entity, int level)
    {
        foreach (var (known, max) in MaxLevels)
        {
            if (known == entity)
            {
                var value = (byte)(sbyte)Math.Clamp(level, sbyte.MinValue, max);
                d[At(entity)] = value;
                d[At(entity) + QuickRaceLevel] = value;
            }
        }
    }

    public void UnlockAll()
    {
        foreach (var (entity, level) in MaxLevels)
        {
            Raise(At(entity), level);
            Raise(At(entity) + QuickRaceLevel, level);
        }
    }

    private void Raise(int at, int level)
    {
        if ((sbyte)d[at] < level)
        {
            d[at] = (byte)level;
        }
    }

    private int At(int entity) => d.Map.Unlocks + entity * Layout.UnlockSize;
}
