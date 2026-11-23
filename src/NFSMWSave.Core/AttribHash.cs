using System.Text;

namespace NFSMWSave.Core;

public static class AttribHash
{
    private const uint Golden = 0x9E3779B9;
    private const uint Seed = 0xABCDEF00;

    public static uint Of(string text)
    {
        var k = Encoding.ASCII.GetBytes(text);
        uint a = Golden, b = Golden, c = Seed;
        var i = 0;
        while (k.Length - i >= 12)
        {
            a += BitConverter.ToUInt32(k, i);
            b += BitConverter.ToUInt32(k, i + 4);
            c += BitConverter.ToUInt32(k, i + 8);
            Mix(ref a, ref b, ref c);
            i += 12;
        }
        c += (uint)k.Length;
        var left = k.Length - i;
        if (left >= 11) c += (uint)k[i + 10] << 24;
        if (left >= 10) c += (uint)k[i + 9] << 16;
        if (left >= 9) c += (uint)k[i + 8] << 8;
        if (left >= 8) b += (uint)k[i + 7] << 24;
        if (left >= 7) b += (uint)k[i + 6] << 16;
        if (left >= 6) b += (uint)k[i + 5] << 8;
        if (left >= 5) b += k[i + 4];
        if (left >= 4) a += (uint)k[i + 3] << 24;
        if (left >= 3) a += (uint)k[i + 2] << 16;
        if (left >= 2) a += (uint)k[i + 1] << 8;
        if (left >= 1) a += k[i];
        Mix(ref a, ref b, ref c);
        return c;
    }

    private static void Mix(ref uint a, ref uint b, ref uint c)
    {
        a -= b + c; a ^= c >> 13;
        b -= c + a; b ^= a << 8;
        c -= a + b; c ^= b >> 13;
        a -= b + c; a ^= c >> 12;
        b -= c + a; b ^= a << 16;
        c -= a + b; c ^= b >> 5;
        a -= b + c; a ^= c >> 3;
        b -= c + a; b ^= a << 10;
        c -= a + b; c ^= b >> 15;
    }
}
