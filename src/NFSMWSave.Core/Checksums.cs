namespace NFSMWSave.Core;

internal static class Checksums
{
    private const uint Polynomial = 0xEDB88320;
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? (value >> 1) ^ Polynomial : value >> 1;
            }
            table[i] = value;
        }
        return table;
    }

    public static uint Crc32(ReadOnlySpan<byte> data)
    {
        var value = 0xFFFFFFFF;
        foreach (var b in data)
        {
            value = Table[(value ^ b) & 0xFF] ^ (value >> 8);
        }
        return ~value;
    }
}
