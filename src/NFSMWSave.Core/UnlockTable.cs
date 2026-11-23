namespace NFSMWSave.Core;

public sealed class UnlockTable(SaveBuffer d)
{
    private static readonly (int Entity, int Level)[] MaxLevels =
    [
        (4, 3), (5, 4), (6, 3), (7, 4), (8, 4), (9, 3), (10, 3),
        (11, 4), (12, 5), (13, 6), (14, 6), (15, 6), (17, 4), (18, 4), (23, 3), (40, 6),
    ];

    public int UnlockedCount => MaxLevels.Count(m => (sbyte)d[At(m.Entity)] >= m.Level);

    public int TotalCount => MaxLevels.Length;

    public IEnumerable<(int Entity, int Level, int MaxLevel)> Levels => MaxLevels.Select(m => (m.Entity, (int)(sbyte)d[At(m.Entity)], m.Level));

    internal void SetLevel(int entity, int level)
    {
        foreach (var (known, max) in MaxLevels)
        {
            if (known == entity)
            {
                d[At(entity)] = (byte)(sbyte)Math.Clamp(level, sbyte.MinValue, max);
            }
        }
    }

    public void UnlockAll()
    {
        foreach (var (entity, level) in MaxLevels)
        {
            if ((sbyte)d[At(entity)] < level)
            {
                d[At(entity)] = (byte)level;
            }
        }
    }

    private int At(int entity) => d.Map.Unlocks + entity * Layout.UnlockSize;
}
