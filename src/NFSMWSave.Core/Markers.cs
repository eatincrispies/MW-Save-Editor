namespace NFSMWSave.Core;

public sealed record MarkerType(int Id, string Name);

public sealed class MarkerInventory(SaveBuffer d)
{
    private const int StateOwned = 1;

    public static readonly IReadOnlyList<MarkerType> Types =
    [
        new(17, "Get Out of Jail"),
        new(20, "Impound Strike"),
        new(21, "Impound Release"),
        new(2, "Engine"),
        new(7, "Transmission"),
        new(5, "Suspension"),
        new(3, "Nitrous"),
        new(6, "Tires"),
        new(1, "Brakes"),
        new(4, "Turbo / Supercharger"),
        new(8, "Body Kit"),
        new(9, "Hood"),
        new(10, "Spoiler"),
        new(11, "Rims"),
        new(12, "Roof Scoop"),
        new(13, "Custom HUD"),
        new(14, "Vinyl"),
        new(15, "Decal"),
        new(16, "Paint"),
    ];

    public int Capacity => Layout.MarkerCount;

    public int UsedSlots
    {
        get
        {
            var used = 0;
            for (var i = 0; i < Layout.MarkerCount; i++)
            {
                used += d.I32(At(i)) != 0 ? 1 : 0;
            }
            return used;
        }
    }

    public int Count(int type)
    {
        var count = 0;
        for (var i = 0; i < Layout.MarkerCount; i++)
        {
            if (d.I32(At(i)) == type && d.I32(At(i) + 8) == StateOwned)
            {
                count++;
            }
        }
        return count;
    }

    public int SetCount(int type, int wanted)
    {
        wanted = Math.Max(wanted, 0);
        var have = Count(type);
        for (var i = Layout.MarkerCount - 1; i >= 0 && have > wanted; i--)
        {
            if (d.I32(At(i)) == type && d.I32(At(i) + 8) == StateOwned)
            {
                d.I32(At(i), 0);
                d.I32(At(i) + 4, 0);
                d.I32(At(i) + 8, 0);
                have--;
            }
        }
        for (var i = 0; i < Layout.MarkerCount && have < wanted; i++)
        {
            if (d.I32(At(i)) == 0)
            {
                d.I32(At(i), type);
                d.I32(At(i) + 4, 0);
                d.I32(At(i) + 8, StateOwned);
                have++;
            }
        }
        return have;
    }

    private int At(int index) => d.Map.Markers + index * Layout.MarkerSize;
}
