using System.Buffers.Binary;

namespace NFSMWSave.Core;

internal static class UnlimiterPresets
{
    private const string FolderName = "Presets";
    private const int FileSize = 0x290;
    private const int VehicleKeyOffset = 0x50;
    private const int PartHashes = 0x60;

    private static readonly int[] EditedSlots =
    [
        CarParts.BodySlot,
        CarParts.SpoilerSlot,
        CarParts.RoofSlot,
        CarParts.HoodSlot,
        CarParts.RimSlot,
        CarPaints.PaintSlot,
        CarPaints.VinylSlot,
        CarPaints.RimPaintSlot,
        CarPaints.VinylColourSlot,
        CarPaints.VinylColourSlot + 1,
        CarPaints.VinylColourSlot + 2,
        CarPaints.VinylColourSlot + 3,
        .. CarParts.VisualSlots,
    ];

    public static void Sync(string profilePath, IEnumerable<CareerCar> cars, Func<ushort, uint?> hashOf, ISet<string> backedUp)
    {
        var profileFolder = Path.GetDirectoryName(Path.GetFullPath(profilePath));
        if (profileFolder is null)
        {
            return;
        }
        var folder = Path.Combine(profileFolder, FolderName);
        if (!Directory.Exists(folder))
        {
            return;
        }
        foreach (var car in cars)
        {
            var file = Path.Combine(folder, $"{car.CustomizationIndex:00}.bin");
            if (File.Exists(file))
            {
                Update(file, car, hashOf, backedUp);
            }
        }
    }

    public static void Retire(string profilePath, IEnumerable<int> customizations)
    {
        var profileFolder = Path.GetDirectoryName(Path.GetFullPath(profilePath));
        if (profileFolder is null)
        {
            return;
        }
        var folder = Path.Combine(profileFolder, FolderName);
        if (!Directory.Exists(folder))
        {
            return;
        }
        foreach (var index in customizations)
        {
            var file = Path.Combine(folder, $"{index:00}.bin");
            if (File.Exists(file))
            {
                File.Move(file, $"{file}.{DateTime.Now:yyyyMMdd-HHmmss}.bak", true);
            }
        }
    }

    private static void Update(string file, CareerCar car, Func<ushort, uint?> hashOf, ISet<string> backedUp)
    {
        var data = File.ReadAllBytes(file);
        if (data.Length != FileSize || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(VehicleKeyOffset)) != car.VehicleKey)
        {
            return;
        }
        var output = (byte[])data.Clone();
        foreach (var slot in EditedSlots)
        {
            var part = car.InstalledPart(slot);
            var hash = part == CarPaints.NoPart ? 0u : hashOf(part);
            if (hash is not null)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(PartHashes + slot * 4), hash.Value);
            }
        }
        var frontBefore = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(PartHashes + CarParts.RimSlot * 4));
        var frontAfter = BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(PartHashes + CarParts.RimSlot * 4));
        var rear = output.AsSpan(PartHashes + CarParts.RearRimSlot * 4);
        if (frontAfter != frontBefore && BinaryPrimitives.ReadUInt32LittleEndian(rear) != 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(rear, frontAfter);
        }
        if (output.AsSpan().SequenceEqual(data))
        {
            return;
        }
        if (backedUp.Add(file))
        {
            File.Copy(file, $"{file}.{DateTime.Now:yyyyMMdd-HHmmss}.bak", true);
        }
        var temp = file + ".tmp";
        File.WriteAllBytes(temp, output);
        File.Move(temp, file, true);
    }
}
