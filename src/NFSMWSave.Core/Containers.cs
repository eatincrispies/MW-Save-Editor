using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace NFSMWSave.Core;

public abstract class SaveContainer
{
    protected SaveContainer(byte[] original)
    {
        Original = original;
    }

    protected byte[] Original { get; }

    public abstract string FormatName { get; }
    public virtual string? ResignNotice => null;
    public virtual string? OpenNotice => null;
    public virtual bool CanRename => true;
    public abstract byte[] ExtractProfile();
    protected abstract byte[] BuildCore(byte[] profile, string alias, bool renamed);

    public byte[] Build(byte[] profile, string alias)
    {
        var current = ExtractProfile();
        var oldAlias = Bytes.Text(current, SaveLayout.Detect(current).ProfileName, Layout.ProfileNameSize);
        return current.AsSpan().SequenceEqual(profile) ? (byte[])Original.Clone() : BuildCore(profile, alias, oldAlias != alias);
    }

    public static SaveContainer Open(byte[] file)
    {
        if (StartsWith(file, MaxContainer.Magic))
        {
            return new MaxContainer(file);
        }
        if (StartsWith(file, CbsContainer.Magic))
        {
            return new CbsContainer(file);
        }
        if (StartsWith(file, StfsContainer.Magic))
        {
            return new StfsContainer(file);
        }
        if (StartsWith(file, PsvContainer.Magic))
        {
            return new PsvContainer(file);
        }
        if (GciContainer.Matches(file))
        {
            return new GciContainer(file);
        }
        if (PsuContainer.Matches(file))
        {
            return new PsuContainer(file);
        }
        if (XboxContainer.Matches(file))
        {
            return new XboxContainer(file);
        }
        return new RawContainer(file);
    }

    private static bool StartsWith(byte[] file, ReadOnlySpan<byte> magic) => file.AsSpan().StartsWith(magic);
}

internal sealed class RawContainer(byte[] file) : SaveContainer(file)
{
    public override string FormatName => SaveLayout.Detect(Original).Platform == SavePlatform.PC ? "PC profile" : "Raw save";

    public override byte[] ExtractProfile()
    {
        var map = SaveLayout.Detect(Original);
        if (Original.Length != map.FileSize)
        {
            throw new SaveFormatException($"Expected {map.FileSize:N0} bytes, found {Original.Length:N0}.");
        }
        return (byte[])Original.Clone();
    }

    protected override byte[] BuildCore(byte[] profile, string alias, bool renamed) => (byte[])profile.Clone();
}

internal sealed class XboxContainer(byte[] file) : SaveContainer(file)
{
    private const int FileSize = 0xF508;
    private const int SignatureExtra = 0x30;
    private const int SignatureSize = 20;
    private const int InfoSignature = 0x10;
    private const int ProfileSignature = 0x24;
    private const int HeaderSignature = 0x38;
    private const int InfoAt = 0x4C;
    private const int ProfileAt = 0x54;
    private const string SigningKeyHex = "4F305E65F960BFFB92CFF59AD099BE67";

    private static readonly byte[][] SigningKeys = [Convert.FromHexString(SigningKeyHex), new byte[16]];

    private readonly byte[]? _matchedKey = FindKey(file);

    private byte[] Key => _matchedKey ?? SigningKeys[0];

    public override string FormatName => "Original Xbox";

    public override string? OpenNotice => _matchedKey is not null ? null : "This save's signature doesn't match any known key. Saving will re-sign it for the USA and Europe games.";

    private static byte[]? FindKey(byte[] data) => SigningKeys.FirstOrDefault(key => SignatureMatches(key, data, InfoAt, Layout.InfoSize, InfoSignature));

    private static bool SignatureMatches(byte[] key, byte[] data, int at, int size, int signature) =>
        data.Length == FileSize && HMACSHA1.HashData(key, data.AsSpan(at, size)).AsSpan().SequenceEqual(data.AsSpan(signature, SignatureSize));

    public static bool Matches(byte[] file) =>
        file.Length == FileSize
        && file.AsSpan().StartsWith("20CM"u8)
        && BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(Layout.ProfileSizeOffset)) == SaveLayout.OriginalXboxProfileSize;

    public override byte[] ExtractProfile()
    {
        var output = new byte[SaveLayout.OriginalXbox.FileSize];
        Original.AsSpan(0, Layout.InfoSignatureOffset).CopyTo(output);
        Original.AsSpan(Layout.InfoOffset + SignatureExtra).CopyTo(output.AsSpan(Layout.InfoOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(Layout.FileLengthOffset), (uint)output.Length);
        var profileGood = SignatureMatches(Key, Original, ProfileAt, SaveLayout.OriginalXboxProfileSize, ProfileSignature);
        var headerGood = SignatureMatches(Key, Original, 0, HeaderSignature, HeaderSignature);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(Layout.InfoSignatureOffset), Integrity.Signature(output.AsSpan(Layout.InfoOffset, Layout.InfoSize)) ^ (_matchedKey is not null ? 0u : 1u));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(Layout.ProfileSignatureOffset), Integrity.Signature(output.AsSpan(Layout.ProfileOffset, SaveLayout.OriginalXboxProfileSize)) ^ (profileGood ? 0u : 1u));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(Layout.HeaderSignatureOffset), Integrity.Signature(output.AsSpan(0, Layout.HeaderSignedSize)) ^ (headerGood ? 0u : 1u));
        return output;
    }

    protected override byte[] BuildCore(byte[] profile, string alias, bool renamed)
    {
        var output = new byte[FileSize];
        profile.AsSpan(0, Layout.InfoSignatureOffset).CopyTo(output);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(Layout.FileLengthOffset), FileSize);
        profile.AsSpan(Layout.InfoOffset).CopyTo(output.AsSpan(InfoAt));
        HMACSHA1.HashData(Key, output.AsSpan(InfoAt, Layout.InfoSize), output.AsSpan(InfoSignature, SignatureSize));
        HMACSHA1.HashData(Key, output.AsSpan(ProfileAt, SaveLayout.OriginalXboxProfileSize), output.AsSpan(ProfileSignature, SignatureSize));
        HMACSHA1.HashData(Key, output.AsSpan(0, HeaderSignature), output.AsSpan(HeaderSignature, SignatureSize));
        return output;
    }
}

internal sealed class GciContainer(byte[] file) : SaveContainer(file)
{
    private const int FileSize = 73792;
    private const int NameOffset = 0x08;
    private const int NameSize = 32;
    private const string NamePrefix = "NFSMW";
    private const int BodyOffset = 0x40;
    private const int SumOffset = BodyOffset + 12;
    private const int ImageSizeOffset = BodyOffset + 8;
    private const int ChecksummedOffset = BodyOffset + 0x10;
    private const int AliasOffset = BodyOffset + 0x30;
    private const int AliasSize = 32;

    public override string FormatName => "GameCube GCI";

    public static bool Matches(byte[] file) =>
        file.Length == FileSize && file[0] == (byte)'G' && file[1] == (byte)'O' && file[2] == (byte)'W' && file[4] == (byte)'6' && file[5] == (byte)'9';

    private int ProfileAt => ChecksummedOffset + BinaryPrimitives.ReadInt32BigEndian(Original.AsSpan(ImageSizeOffset));

    public override byte[] ExtractProfile() => Original.AsSpan(ProfileAt, SaveLayout.GameCube.FileSize).ToArray();

    protected override byte[] BuildCore(byte[] profile, string alias, bool renamed)
    {
        var output = (byte[])Original.Clone();
        profile.CopyTo(output, ProfileAt);
        if (renamed)
        {
            Bytes.WriteText(output, NameOffset, NameSize, NamePrefix + alias);
            Bytes.WriteText(output, AliasOffset, AliasSize, alias);
        }
        var sum = 0;
        for (var i = ChecksummedOffset; i < ProfileAt; i++)
        {
            sum += (sbyte)output[i];
        }
        BinaryPrimitives.WriteInt32BigEndian(output.AsSpan(SumOffset), sum);
        return output;
    }
}

internal sealed record Ps2Entry(byte[] Header, string Name, byte[] Data);

internal static class Ps2Names
{
    public const string DirectoryPrefix = "BASLUS-21351-";
    private const int IconTitleOffset = 0xC0;
    private const int IconTitleSize = 68;
    private const int IconPrefixBytes = 12;

    public static bool IsProfile(string name, byte[] data) =>
        (data.Length == SaveLayout.Ps2.FileSize || data.Length == SaveLayout.Ps2Early.FileSize) && data.AsSpan().StartsWith("20CM"u8) && name.Contains('-');

    public static string ProfileName(string oldName, string alias)
    {
        var dash = oldName.LastIndexOf('-');
        return dash < 0 ? DirectoryPrefix + alias : oldName[..(dash + 1)] + alias;
    }

    public static void RetitleIcon(byte[] icon, string alias)
    {
        if (icon.Length < IconTitleOffset + IconTitleSize || !icon.AsSpan().StartsWith("PS2D"u8))
        {
            return;
        }
        var area = icon.AsSpan(IconTitleOffset + IconPrefixBytes, IconTitleSize - IconPrefixBytes);
        area.Clear();
        var at = 0;
        foreach (var c in alias)
        {
            var code = c switch
            {
                >= 'A' and <= 'Z' => 0x8260 + (c - 'A'),
                >= 'a' and <= 'z' => 0x8281 + (c - 'a'),
                _ => 0x824F + (c - '0'),
            };
            area[at++] = (byte)(code >> 8);
            area[at++] = (byte)code;
        }
    }
}

internal sealed class PsuContainer : SaveContainer
{
    private const int EntrySize = 512;
    private const int ModeFile = 0x10;
    private const int ModeDirectory = 0x20;
    private const int LengthOffset = 4;
    private const int NameOffset = 0x40;
    private const int NameSize = 32;
    private const int DataAlignment = 1024;
    private const int MaxEntries = 64;

    private readonly List<(int Header, int Data, int Size, string Name)> _files = [];

    public PsuContainer(byte[] file) : base(file)
    {
        var count = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(LengthOffset));
        if (count is < 2 or > MaxEntries)
        {
            throw new SaveFormatException("This PS2 save is damaged.");
        }
        var at = EntrySize;
        for (var i = 0; i < count; i++)
        {
            if (at + EntrySize > file.Length)
            {
                throw new SaveFormatException("This PS2 save is damaged.");
            }
            var mode = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(at));
            var size = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(at + LengthOffset));
            var name = Bytes.Text(file, at + NameOffset, NameSize);
            var data = at + EntrySize;
            at = data;
            if ((mode & ModeFile) == 0)
            {
                continue;
            }
            if (size < 0 || data + size > file.Length)
            {
                throw new SaveFormatException("This PS2 save is damaged.");
            }
            _files.Add((at - EntrySize, data, size, name));
            at = data + ((size + DataAlignment - 1) & ~(DataAlignment - 1));
        }
    }

    public override string FormatName => "PS2 PSU";

    public static bool Matches(byte[] file)
    {
        if (file.Length < EntrySize * 3)
        {
            return false;
        }
        var mode = BinaryPrimitives.ReadUInt16LittleEndian(file);
        return (mode & ModeDirectory) != 0
            && Bytes.Text(file, EntrySize + NameOffset, NameSize) == "."
            && Bytes.Text(file, EntrySize * 2 + NameOffset, NameSize) == "..";
    }

    private (int Header, int Data, int Size, string Name) ProfileEntry()
    {
        foreach (var entry in _files)
        {
            if (Ps2Names.IsProfile(entry.Name, Original.AsSpan(entry.Data, entry.Size).ToArray()))
            {
                return entry;
            }
        }
        throw new SaveFormatException("No Most Wanted profile was found in this PS2 save.");
    }

    public override byte[] ExtractProfile()
    {
        var entry = ProfileEntry();
        return Original.AsSpan(entry.Data, entry.Size).ToArray();
    }

    protected override byte[] BuildCore(byte[] profile, string alias, bool renamed)
    {
        var output = (byte[])Original.Clone();
        var entry = ProfileEntry();
        profile.CopyTo(output, entry.Data);
        if (renamed)
        {
            var newName = Ps2Names.ProfileName(entry.Name, alias);
            Bytes.WriteText(output, entry.Header + NameOffset, NameSize, newName);
            Bytes.WriteText(output, NameOffset, NameSize, Ps2Names.ProfileName(Bytes.Text(output, NameOffset, NameSize), alias));
            foreach (var file in _files.Where(f => f.Name == "icon.sys"))
            {
                var icon = output.AsSpan(file.Data, file.Size).ToArray();
                Ps2Names.RetitleIcon(icon, alias);
                icon.CopyTo(output, file.Data);
            }
        }
        return output;
    }
}

internal sealed class PsvContainer : SaveContainer
{
    private const int SeedOffset = 0x08;
    private const int SeedSize = 0x14;
    private const int SignatureOffset = 0x1C;
    private const int SignatureSize = 0x14;
    private const int TypeOffset = 0x3C;
    private const int Ps2Type = 2;
    private const int FileCountOffset = 0x64;
    private const int FirstEntry = 0xA0;
    private const int EntrySize = 0x3C;
    private const int EntryLength = 0x10;
    private const int EntryName = 0x18;
    private const int NameSize = 32;
    private const int EntryData = 0x38;
    private const int MaxFiles = 64;
    private const int SaltSize = 0x40;

    private static readonly byte[] Key = [0xEA, 0x02, 0xCE, 0xEF, 0x5B, 0xB4, 0xD2, 0x99, 0x8F, 0x61, 0x19, 0x10, 0xD7, 0x7F, 0x51, 0xC6];
    private static readonly byte[] Iv = [0xB3, 0x0F, 0xFE, 0xED, 0xB7, 0xDC, 0x5E, 0xB7, 0x13, 0x3D, 0xA6, 0x0D, 0x1B, 0x6B, 0x2C, 0xDC];

    private readonly List<(int Data, int Size, string Name)> _files = [];
    private readonly bool _signed;

    public PsvContainer(byte[] file) : base(file)
    {
        if (file.Length < FirstEntry || BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(TypeOffset)) != Ps2Type)
        {
            throw new SaveFormatException("This PSV file isn't a PS2 save.");
        }
        var count = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(FileCountOffset));
        if (count is < 1 or > MaxFiles || FirstEntry + count * EntrySize > file.Length)
        {
            throw new SaveFormatException("This PSV file is damaged.");
        }
        for (var i = 0; i < count; i++)
        {
            var at = FirstEntry + i * EntrySize;
            var size = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(at + EntryLength));
            var data = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(at + EntryData));
            if (size < 0 || data < 0 || (long)data + size > file.Length)
            {
                throw new SaveFormatException("This PSV file is damaged.");
            }
            _files.Add((data, size, Bytes.Text(file, at + EntryName, NameSize)));
        }
        _signed = file.AsSpan(SignatureOffset, SignatureSize).SequenceEqual(Signature(file));
    }

    public static ReadOnlySpan<byte> Magic => "\0VSP"u8;

    public override string FormatName => "PS3 PSV";

    public override bool CanRename => false;

    public override string? OpenNotice => _signed ? null : "This PSV file's signature doesn't match. Saving will sign it again so the PS3 accepts it.";

    private (int Data, int Size, string Name) ProfileEntry()
    {
        foreach (var entry in _files)
        {
            if (Ps2Names.IsProfile(entry.Name, Original.AsSpan(entry.Data, entry.Size).ToArray()))
            {
                return entry;
            }
        }
        throw new SaveFormatException("No Most Wanted profile was found in this PSV file.");
    }

    public override byte[] ExtractProfile()
    {
        var entry = ProfileEntry();
        return Original.AsSpan(entry.Data, entry.Size).ToArray();
    }

    protected override byte[] BuildCore(byte[] profile, string alias, bool renamed)
    {
        var output = (byte[])Original.Clone();
        var entry = ProfileEntry();
        if (profile.Length != entry.Size)
        {
            throw new SaveFormatException("The edited profile no longer fits this PSV file.");
        }
        profile.CopyTo(output, entry.Data);
        Signature(output).CopyTo(output, SignatureOffset);
        return output;
    }

    private static byte[] Signature(byte[] file)
    {
        var salt = new byte[SaltSize];
        file.AsSpan(SeedOffset, SeedSize).CopyTo(salt);
        using (var aes = Aes.Create())
        {
            aes.Key = Key;
            salt = aes.DecryptCbc(salt, Iv, PaddingMode.None);
        }
        salt.AsSpan(SeedSize).Clear();
        var body = (byte[])file.Clone();
        body.AsSpan(SignatureOffset, SignatureSize).Clear();
        return HMACSHA1.HashData(salt, body);
    }
}

internal sealed class MaxContainer : SaveContainer
{
    public static ReadOnlySpan<byte> Magic => "Ps2PowerSave"u8;
    private const int CrcOffset = 12;
    private const int DirectoryOffset = 16;
    private const int TitleOffset = 48;
    private const int NameSize = 32;
    private const string TitlePrefix = "NFS MW";
    private const int CompressedSizeOffset = 80;
    private const int FileCountOffset = 84;
    private const int LengthOffset = 88;
    private const int StreamOffset = 92;
    private const int EntryHeaderSize = 36;

    private readonly List<Ps2Entry> _entries = [];

    public MaxContainer(byte[] file) : base(file)
    {
        var count = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(FileCountOffset));
        var length = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(LengthOffset));
        if (count is < 1 or > 64 || length is < 1 or > 16_000_000)
        {
            throw new SaveFormatException("This PS2 save is damaged.");
        }
        var body = Lzari.Decode(file.AsSpan(StreamOffset), length);
        var o = 0;
        for (var i = 0; i < count; i++)
        {
            var size = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(o));
            if (size < 0 || o + EntryHeaderSize + size > body.Length)
            {
                throw new SaveFormatException("This PS2 save is damaged.");
            }
            var name = Bytes.Text(body, o + 4, NameSize);
            _entries.Add(new Ps2Entry(body.AsSpan(o, EntryHeaderSize).ToArray(), name, body.AsSpan(o + EntryHeaderSize, size).ToArray()));
            o += EntryHeaderSize + size;
            o = ((o + 8 + 15) & ~15) - 8;
        }
    }

    public override string FormatName => "PS2 MAX";

    public override byte[] ExtractProfile() =>
        _entries.FirstOrDefault(e => Ps2Names.IsProfile(e.Name, e.Data))?.Data.ToArray() ?? throw new SaveFormatException("No Most Wanted profile was found in this PS2 save.");

    protected override byte[] BuildCore(byte[] profile, string alias, bool renamed)
    {
        using var body = new MemoryStream();
        var newName = "";
        foreach (var entry in _entries)
        {
            var header = (byte[])entry.Header.Clone();
            var data = entry.Data;
            if (Ps2Names.IsProfile(entry.Name, entry.Data))
            {
                if (renamed)
                {
                    newName = Ps2Names.ProfileName(entry.Name, alias);
                    Bytes.WriteText(header, 4, NameSize, newName);
                }
                data = profile;
            }
            else if (renamed && entry.Name == "icon.sys")
            {
                data = (byte[])entry.Data.Clone();
                Ps2Names.RetitleIcon(data, alias);
            }
            body.Write(header);
            body.Write(data);
            var end = (int)body.Position;
            body.Write(new byte[(((end + 8 + 15) & ~15) - 8) - end]);
        }
        var plain = body.ToArray();
        var compressed = Lzari.Encode(plain);
        var output = new byte[StreamOffset + compressed.Length];
        Original.AsSpan(0, StreamOffset).CopyTo(output);
        compressed.CopyTo(output, StreamOffset);
        if (renamed)
        {
            Bytes.WriteText(output, DirectoryOffset, NameSize, newName);
            Bytes.WriteText(output, TitleOffset, NameSize, TitlePrefix + alias);
        }
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(CompressedSizeOffset), compressed.Length + 4);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(LengthOffset), plain.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(CrcOffset), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(CrcOffset), Checksums.Crc32(output));
        return output;
    }
}

internal sealed class CbsContainer : SaveContainer
{
    public static ReadOnlySpan<byte> Magic => "CFU\0"u8;
    private const int HeaderLengthOffset = 8;
    private const int PlainLengthOffset = 12;
    private const int FileLengthOffset = 16;
    private const int DirectoryOffset = 20;
    private const int DirectorySize = 32;
    private const int TitleOffset = 92;
    private const string TitlePrefix = "NFS MW ";
    private const int EntryHeaderSize = 64;
    private const int EntrySizeOffset = 16;
    private const int EntryNameOffset = 32;
    private const string StateHex = "5f1f856f31aa3b1821b9ce1c074c9cb481b8ef9859aef926e380a3292d7351627c6446f4341af6e1ba3a0d82790a5c1671498eac8c9f351945943f560c91000bd7b0dd3966a176521357f3bb4ee5dcf06584b2d6df153c631d8914bdd236feb1ca8ba4c69e674737426d6a039270057d962f4090c4f13e3d01f7681ec3fc72b554cfe741e44d835512220978fadea7060823bf0fccc19761c54ae6a011c2ea740287d5d19db77e386053958d2577105e9b7fd86edaa22e204fcd8fcbbe5ae0ed2c9ad4e2afd0a9e8ad7abca8f2eeebf5a69928246c2b755df8d38617fbc07bb358dbc74bff0450e98869c92aabfd5b1b8ad9ec27440e33c86b933248b63043a5";

    private readonly int _headerLength;
    private readonly List<Ps2Entry> _entries = [];

    public CbsContainer(byte[] file) : base(file)
    {
        _headerLength = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(HeaderLengthOffset));
        if (_headerLength < TitleOffset || _headerLength >= file.Length)
        {
            throw new SaveFormatException("This PS2 save is damaged.");
        }
        byte[] body;
        try
        {
            using var input = new MemoryStream(Crypt(file.AsSpan(_headerLength)));
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var plain = new MemoryStream();
            zlib.CopyTo(plain);
            body = plain.ToArray();
        }
        catch (InvalidDataException)
        {
            throw new SaveFormatException("This PS2 save is damaged.");
        }
        var o = 0;
        while (o + EntryHeaderSize <= body.Length)
        {
            var size = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(o + EntrySizeOffset));
            if (size < 0 || o + EntryHeaderSize + size > body.Length)
            {
                throw new SaveFormatException("This PS2 save is damaged.");
            }
            var name = Bytes.Text(body, o + EntryNameOffset, DirectorySize);
            _entries.Add(new Ps2Entry(body.AsSpan(o, EntryHeaderSize).ToArray(), name, body.AsSpan(o + EntryHeaderSize, size).ToArray()));
            o += EntryHeaderSize + size;
        }
    }

    public override string FormatName => "PS2 CBS";

    public override byte[] ExtractProfile() =>
        _entries.FirstOrDefault(e => Ps2Names.IsProfile(e.Name, e.Data))?.Data.ToArray() ?? throw new SaveFormatException("No Most Wanted profile was found in this PS2 save.");

    protected override byte[] BuildCore(byte[] profile, string alias, bool renamed)
    {
        using var body = new MemoryStream();
        var newName = "";
        foreach (var entry in _entries)
        {
            var header = (byte[])entry.Header.Clone();
            var data = entry.Data;
            if (Ps2Names.IsProfile(entry.Name, entry.Data))
            {
                if (renamed)
                {
                    newName = Ps2Names.ProfileName(entry.Name, alias);
                    Bytes.WriteText(header, EntryNameOffset, DirectorySize, newName);
                }
                data = profile;
            }
            else if (renamed && entry.Name == "icon.sys")
            {
                data = (byte[])entry.Data.Clone();
                Ps2Names.RetitleIcon(data, alias);
            }
            body.Write(header);
            body.Write(data);
        }
        var plain = body.ToArray();
        using var packed = new MemoryStream();
        using (var zlib = new ZLibStream(packed, CompressionLevel.SmallestSize, true))
        {
            zlib.Write(plain);
        }
        var cipher = Crypt(packed.ToArray());
        var output = new byte[_headerLength + cipher.Length];
        Original.AsSpan(0, _headerLength).CopyTo(output);
        cipher.CopyTo(output, _headerLength);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(PlainLengthOffset), plain.Length);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(FileLengthOffset), output.Length);
        if (renamed)
        {
            Bytes.WriteText(output, DirectoryOffset, DirectorySize, newName);
            Bytes.WriteText(output, TitleOffset, _headerLength - TitleOffset, TitlePrefix + alias);
        }
        return output;
    }

    private static byte[] Crypt(ReadOnlySpan<byte> data)
    {
        var s = Convert.FromHexString(StateHex);
        var output = new byte[data.Length];
        int i = 0, j = 0;
        for (var k = 0; k < data.Length; k++)
        {
            i = (i + 1) & 255;
            j = (j + s[i]) & 255;
            (s[i], s[j]) = (s[j], s[i]);
            output[k] = (byte)(data[k] ^ s[(s[i] + s[j]) & 255]);
        }
        return output;
    }
}

internal sealed class StfsContainer : SaveContainer
{
    public static ReadOnlySpan<byte> Magic => "CON "u8;
    private const int BlockSize = 0x1000;
    private const int ContentHashOffset = 0x32C;
    private const int HashedStart = 0x344;
    private const int HeaderSizeOffset = 0x340;
    private const int DescriptorOffset = 0x379;
    private const int FileTableBlockOffset = DescriptorOffset + 5;
    private const int TopHashOffset = DescriptorOffset + 8;
    private const int AllocatedOffset = DescriptorOffset + 0x1C;
    private const int HashSize = 20;
    private const int EntrySize = 24;
    private const int NextOffset = 21;
    private const int MaxSingleTableBlocks = 170;
    private const int NameSize = 0x28;
    private const int NameFlagsOffset = 0x28;
    private const byte NameLengthMask = 0x3F;
    private const int StartBlockOffset = 0x2F;
    private const int FileSizeOffset = 0x34;

    private readonly int _table;
    private readonly int _firstBlock;
    private readonly int _fileTable;
    private readonly int _allocated;

    public StfsContainer(byte[] file) : base(file)
    {
        var headerSize = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(HeaderSizeOffset));
        var aligned = (headerSize + BlockSize - 1) & ~(BlockSize - 1);
        _allocated = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(AllocatedOffset));
        if (_allocated is < 1 or > MaxSingleTableBlocks || aligned + 2 * BlockSize > file.Length)
        {
            throw new SaveFormatException("This Xbox 360 package has an unsupported layout.");
        }
        var top = file.AsSpan(TopHashOffset, HashSize);
        if (SHA1.HashData(file.AsSpan(aligned, BlockSize)).AsSpan().SequenceEqual(top))
        {
            _table = aligned;
        }
        else if (SHA1.HashData(file.AsSpan(aligned + BlockSize, BlockSize)).AsSpan().SequenceEqual(top))
        {
            _table = aligned + BlockSize;
        }
        else
        {
            throw new SaveFormatException("This Xbox 360 package failed its hash check.");
        }
        _firstBlock = aligned + 2 * BlockSize;
        var tableBlock = file[FileTableBlockOffset] | (file[FileTableBlockOffset + 1] << 8) | (file[FileTableBlockOffset + 2] << 16);
        _fileTable = _firstBlock + tableBlock * BlockSize;
    }

    public override string FormatName => "Xbox 360 CON";

    public override string ResignNotice => "Saved. Xenia can load this package as is. A real Xbox 360 needs it resigned first, with a tool like Horizon.";

    public override byte[] ExtractProfile()
    {
        var size = BinaryPrimitives.ReadInt32BigEndian(Original.AsSpan(_fileTable + FileSizeOffset));
        if (size != SaveLayout.Xbox360.FileSize)
        {
            throw new SaveFormatException("This Xbox 360 package does not hold a Most Wanted profile.");
        }
        var output = new byte[size];
        var written = 0;
        foreach (var block in Chain(Original))
        {
            var count = Math.Min(BlockSize, size - written);
            Original.AsSpan(_firstBlock + block * BlockSize, count).CopyTo(output.AsSpan(written));
            written += count;
        }
        if (written != size)
        {
            throw new SaveFormatException("This Xbox 360 package is damaged.");
        }
        return output;
    }

    private List<int> Chain(byte[] file)
    {
        var blocks = new List<int>();
        var block = file[_fileTable + StartBlockOffset] | (file[_fileTable + StartBlockOffset + 1] << 8) | (file[_fileTable + StartBlockOffset + 2] << 16);
        var needed = (BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(_fileTable + FileSizeOffset)) + BlockSize - 1) / BlockSize;
        while (blocks.Count < needed)
        {
            if (block < 0 || block >= _allocated || _firstBlock + (block + 1) * BlockSize > file.Length)
            {
                throw new SaveFormatException("This Xbox 360 package is damaged.");
            }
            blocks.Add(block);
            var entry = _table + block * EntrySize;
            block = (file[entry + NextOffset] << 16) | (file[entry + NextOffset + 1] << 8) | file[entry + NextOffset + 2];
        }
        return blocks;
    }

    protected override byte[] BuildCore(byte[] profile, string alias, bool renamed)
    {
        var output = (byte[])Original.Clone();
        var written = 0;
        foreach (var block in Chain(Original))
        {
            var count = Math.Min(BlockSize, profile.Length - written);
            var at = _firstBlock + block * BlockSize;
            profile.AsSpan(written, count).CopyTo(output.AsSpan(at));
            written += count;
            SHA1.HashData(output.AsSpan(at, BlockSize), output.AsSpan(_table + block * EntrySize, HashSize));
        }
        
        if (renamed)
        {
            Bytes.WriteText(output, _fileTable, NameSize, alias);
            output[_fileTable + NameFlagsOffset] = (byte)((output[_fileTable + NameFlagsOffset] & ~NameLengthMask) | alias.Length);
            var tableBlock = (_fileTable - _firstBlock) / BlockSize;
            SHA1.HashData(output.AsSpan(_fileTable, BlockSize), output.AsSpan(_table + tableBlock * EntrySize, HashSize));
        }
        SHA1.HashData(output.AsSpan(_table, BlockSize), output.AsSpan(TopHashOffset, HashSize));
        var headerSize = BinaryPrimitives.ReadInt32BigEndian(output.AsSpan(HeaderSizeOffset));
        var aligned = (headerSize + BlockSize - 1) & ~(BlockSize - 1);
        SHA1.HashData(output.AsSpan(HashedStart, aligned - HashedStart), output.AsSpan(ContentHashOffset, HashSize));
        return output;
    }
}
