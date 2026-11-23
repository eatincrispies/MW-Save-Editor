namespace NFSMWSave.Core;

internal sealed class Lzari
{
    private const int WindowSize = 4096;
    private const int MaxMatch = 60;
    private const int Threshold = 2;
    private const int CharCount = 256 - Threshold + MaxMatch;
    private const int MaxDistance = WindowSize - MaxMatch - 1;
    private const int ChainLimit = 256;
    private const long Q1 = 1L << 15;
    private const long Q2 = 2L << 15;
    private const long Q3 = 3L << 15;
    private const long Q4 = 4L << 15;
    private const int MaxCumulative = (int)Q1 - 1;

    private readonly int[] _charToSym = new int[CharCount];
    private readonly int[] _symToChar = new int[CharCount + 1];
    private readonly int[] _symFreq = new int[CharCount + 1];
    private readonly int[] _symCum = new int[CharCount + 1];
    private readonly int[] _posCum = new int[WindowSize + 1];

    private Lzari()
    {
        for (var sym = CharCount; sym >= 1; sym--)
        {
            var ch = sym - 1;
            _charToSym[ch] = sym;
            _symToChar[sym] = ch;
            _symFreq[sym] = 1;
            _symCum[sym - 1] = _symCum[sym] + 1;
        }
        for (var i = WindowSize; i >= 1; i--)
        {
            _posCum[i - 1] = _posCum[i] + 10000 / (i + 200);
        }
    }

    public static byte[] Decode(ReadOnlySpan<byte> source, int length) => new Lzari().DecodeCore(source, length);

    public static byte[] Encode(ReadOnlySpan<byte> source) => new Lzari().EncodeCore(source);

    private void Update(int sym)
    {
        if (_symCum[0] >= MaxCumulative)
        {
            var c = 0;
            for (var i = CharCount; i > 0; i--)
            {
                _symCum[i] = c;
                c += _symFreq[i] = (_symFreq[i] + 1) >> 1;
            }
            _symCum[0] = c;
        }
        var at = sym;
        while (_symFreq[at] == _symFreq[at - 1])
        {
            at--;
        }
        if (at < sym)
        {
            var chAt = _symToChar[at];
            var chSym = _symToChar[sym];
            _symToChar[at] = chSym;
            _symToChar[sym] = chAt;
            _charToSym[chAt] = sym;
            _charToSym[chSym] = at;
        }
        _symFreq[at]++;
        while (--at >= 0)
        {
            _symCum[at]++;
        }
    }

    private int FindSymbol(int x)
    {
        int i = 1, j = CharCount;
        while (i < j)
        {
            var k = (i + j) / 2;
            if (_symCum[k] > x)
            {
                i = k + 1;
            }
            else
            {
                j = k;
            }
        }
        return i;
    }

    private int FindPosition(int x)
    {
        int i = 1, j = WindowSize;
        while (i < j)
        {
            var k = (i + j) / 2;
            if (_posCum[k] > x)
            {
                i = k + 1;
            }
            else
            {
                j = k;
            }
        }
        return i - 1;
    }

    private byte[] DecodeCore(ReadOnlySpan<byte> source, int length)
    {
        var output = new byte[length];
        var window = new byte[WindowSize];
        Array.Fill(window, (byte)' ');
        var r = WindowSize - MaxMatch;
        var produced = 0;
        var bit = 0L;
        long low = 0, high = Q4, value = 0;

        int Next(ReadOnlySpan<byte> s)
        {
            var at = (int)(bit >> 3);
            var v = at < s.Length ? (s[at] >> (7 - (int)(bit & 7))) & 1 : 0;
            bit++;
            return v;
        }

        for (var i = 0; i < 17; i++)
        {
            value = value * 2 + Next(source);
        }

        while (produced < length)
        {
            var range = high - low;
            var sym = FindSymbol((int)(((value - low + 1) * _symCum[0] - 1) / range));
            high = low + range * _symCum[sym - 1] / _symCum[0];
            low += range * _symCum[sym] / _symCum[0];
            Normalize(source);
            var c = _symToChar[sym];
            Update(sym);
            if (c < 256)
            {
                output[produced++] = (byte)c;
                window[r] = (byte)c;
                r = (r + 1) & (WindowSize - 1);
                continue;
            }
            range = high - low;
            var position = FindPosition((int)(((value - low + 1) * _posCum[0] - 1) / range));
            high = low + range * _posCum[position] / _posCum[0];
            low += range * _posCum[position + 1] / _posCum[0];
            Normalize(source);
            var from = (r - position - 1) & (WindowSize - 1);
            var count = c - 255 + Threshold;
            for (var k = 0; k < count && produced < length; k++)
            {
                var b = window[(from + k) & (WindowSize - 1)];
                output[produced++] = b;
                window[r] = b;
                r = (r + 1) & (WindowSize - 1);
            }
        }
        return output;

        void Normalize(ReadOnlySpan<byte> s)
        {
            while (true)
            {
                if (low >= Q2)
                {
                    value -= Q2;
                    low -= Q2;
                    high -= Q2;
                }
                else if (low >= Q1 && high <= Q3)
                {
                    value -= Q1;
                    low -= Q1;
                    high -= Q1;
                }
                else if (high > Q2)
                {
                    break;
                }
                low += low;
                high += high;
                value = 2 * value + Next(s);
            }
        }
    }

    private byte[] EncodeCore(ReadOnlySpan<byte> source)
    {
        var writer = new BitWriter();
        long low = 0, high = Q4;
        var shifts = 0;
        var heads = new int[1 << 16];
        Array.Fill(heads, -1);
        var chain = new int[source.Length];

        void Emit(int bit)
        {
            writer.Put(bit);
            for (; shifts > 0; shifts--)
            {
                writer.Put(1 - bit);
            }
        }

        void Settle()
        {
            while (true)
            {
                if (high <= Q2)
                {
                    Emit(0);
                }
                else if (low >= Q2)
                {
                    Emit(1);
                    low -= Q2;
                    high -= Q2;
                }
                else if (low >= Q1 && high <= Q3)
                {
                    shifts++;
                    low -= Q1;
                    high -= Q1;
                }
                else
                {
                    break;
                }
                low += low;
                high += high;
            }
        }

        void PutChar(int ch)
        {
            var sym = _charToSym[ch];
            var range = high - low;
            high = low + range * _symCum[sym - 1] / _symCum[0];
            low += range * _symCum[sym] / _symCum[0];
            Settle();
            Update(sym);
        }

        void PutPosition(int position)
        {
            var range = high - low;
            high = low + range * _posCum[position] / _posCum[0];
            low += range * _posCum[position + 1] / _posCum[0];
            Settle();
        }

        static int Key(ReadOnlySpan<byte> s, int i) => ((s[i] << 8) ^ (s[i + 1] << 4) ^ s[i + 2]) & 0xFFFF;

        var pos = 0;
        while (pos < source.Length)
        {
            var bestLength = 0;
            var bestDistance = 0;
            if (pos + Threshold < source.Length)
            {
                var candidate = heads[Key(source, pos)];
                var limit = Math.Min(MaxMatch, source.Length - pos);
                for (var tries = 0; candidate >= 0 && pos - candidate <= MaxDistance && tries < ChainLimit; tries++)
                {
                    var n = 0;
                    while (n < limit && source[candidate + n] == source[pos + n])
                    {
                        n++;
                    }
                    if (n > bestLength)
                    {
                        bestLength = n;
                        bestDistance = pos - candidate;
                        if (n == limit)
                        {
                            break;
                        }
                    }
                    candidate = chain[candidate];
                }
            }
            int advance;
            if (bestLength > Threshold)
            {
                PutChar(255 - Threshold + bestLength);
                PutPosition(bestDistance - 1);
                advance = bestLength;
            }
            else
            {
                PutChar(source[pos]);
                advance = 1;
            }
            for (var i = 0; i < advance; i++, pos++)
            {
                if (pos + Threshold < source.Length)
                {
                    var key = Key(source, pos);
                    chain[pos] = heads[key];
                    heads[key] = pos;
                }
            }
        }
        shifts++;
        Emit(low < Q1 ? 0 : 1);
        return writer.Finish();
    }

    private sealed class BitWriter
    {
        private readonly MemoryStream _out = new();
        private int _current;
        private int _count;

        public void Put(int bit)
        {
            _current = (_current << 1) | bit;
            if (++_count == 8)
            {
                _out.WriteByte((byte)_current);
                _current = 0;
                _count = 0;
            }
        }

        public byte[] Finish()
        {
            for (var i = 0; i < 7; i++)
            {
                Put(0);
            }
            return _out.ToArray();
        }
    }
}
