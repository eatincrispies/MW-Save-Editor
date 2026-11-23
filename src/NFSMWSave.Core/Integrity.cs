using System.Security.Cryptography;

namespace NFSMWSave.Core;

public sealed record IntegrityReport(bool Header, bool Info, bool Profile, bool ProfileHash, bool GameplayHash)
{
    public bool AllValid => Header && Info && Profile && ProfileHash && GameplayHash;
}

public static class Integrity
{
    private const uint Polynomial = 0x04C11DB7;
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var value = i << 24;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 0x80000000) != 0 ? (value << 1) ^ Polynomial : value << 1;
            }
            table[i] = value;
        }
        return table;
    }

    public static uint Signature(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
        {
            return 0;
        }
        var value = ~(((uint)data[0] << 24) | ((uint)data[1] << 16) | ((uint)data[2] << 8) | data[3]);
        foreach (var next in data[4..])
        {
            value = ((value << 8) | next) ^ Table[value >> 24];
        }
        return ~value;
    }

    public static IntegrityReport Check(SaveBuffer b)
    {
        var d = b.Data;
        var hashAt = b.Map.ProfileHashOffset;
        var gameplay = MD5.HashData(d.AsSpan(Layout.GameplayOffset + Layout.HashSize, Layout.GameplaySize - Layout.HashSize));
        var profile = MD5.HashData(d.AsSpan(Layout.ProfileHashedOffset, hashAt - Layout.ProfileHashedOffset));
        return new IntegrityReport(
            b.U32(Layout.HeaderSignatureOffset) == Signature(d.AsSpan(0, Layout.HeaderSignedSize)),
            b.U32(Layout.InfoSignatureOffset) == Signature(d.AsSpan(Layout.InfoOffset, Layout.InfoSize)),
            b.U32(Layout.ProfileSignatureOffset) == Signature(d.AsSpan(Layout.ProfileOffset, b.Map.ProfileSize)),
            profile.AsSpan().SequenceEqual(d.AsSpan(hashAt, Layout.HashSize)),
            gameplay.AsSpan().SequenceEqual(d.AsSpan(Layout.GameplayOffset, Layout.HashSize)));
    }

    public static void Seal(SaveBuffer b)
    {
        var d = b.Data;
        var hashAt = b.Map.ProfileHashOffset;
        MD5.HashData(d.AsSpan(Layout.GameplayOffset + Layout.HashSize, Layout.GameplaySize - Layout.HashSize), d.AsSpan(Layout.GameplayOffset, Layout.HashSize));
        MD5.HashData(d.AsSpan(Layout.ProfileHashedOffset, hashAt - Layout.ProfileHashedOffset), d.AsSpan(hashAt, Layout.HashSize));
        b.U32(Layout.InfoSignatureOffset, Signature(d.AsSpan(Layout.InfoOffset, Layout.InfoSize)));
        b.U32(Layout.ProfileSignatureOffset, Signature(d.AsSpan(Layout.ProfileOffset, b.Map.ProfileSize)));
        b.U32(Layout.HeaderSignatureOffset, Signature(d.AsSpan(0, Layout.HeaderSignedSize)));
    }
}
