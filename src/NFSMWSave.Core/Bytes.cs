using System.Buffers.Binary;
using System.Text;

namespace NFSMWSave.Core;

public sealed class SaveBuffer(byte[] data, SaveLayout map)
{
    public byte[] Data { get; } = data;
    public SaveLayout Map { get; } = map;

    public byte this[int offset]
    {
        get => Data[offset];
        set => Data[offset] = value;
    }

    public uint U32(int o) => Map.BigEndian ? BinaryPrimitives.ReadUInt32BigEndian(Data.AsSpan(o)) : BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(o));
    public int I32(int o) => (int)U32(o);
    public ushort U16(int o) => Map.BigEndian ? BinaryPrimitives.ReadUInt16BigEndian(Data.AsSpan(o)) : BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(o));
    public float F32(int o) => BitConverter.UInt32BitsToSingle(U32(o));

    public void U32(int o, uint v)
    {
        if (Map.BigEndian)
        {
            BinaryPrimitives.WriteUInt32BigEndian(Data.AsSpan(o), v);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(Data.AsSpan(o), v);
        }
    }

    public void I32(int o, int v) => U32(o, (uint)v);

    public void U16(int o, ushort v)
    {
        if (Map.BigEndian)
        {
            BinaryPrimitives.WriteUInt16BigEndian(Data.AsSpan(o), v);
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(Data.AsSpan(o), v);
        }
    }

    public void F32(int o, float v) => U32(o, BitConverter.SingleToUInt32Bits(v));

    public bool Bool(int o, int size) => size == 1 ? Data[o] != 0 : U32(o) != 0;

    public void Bool(int o, int size, bool value)
    {
        if (size == 1)
        {
            Data[o] = (byte)(value ? 1 : 0);
        }
        else
        {
            U32(o, value ? 1u : 0u);
        }
    }

    public string Text(int o, int size) => Bytes.Text(Data, o, size);

    public void Text(int o, int size, string value) => Bytes.WriteText(Data, o, size, value);
}

internal static class Bytes
{
    public static string Text(byte[] d, int o, int size)
    {
        var span = d.AsSpan(o, size);
        var end = span.IndexOf((byte)0);
        return Encoding.Latin1.GetString(end < 0 ? span : span[..end]);
    }

    public static void WriteText(byte[] d, int o, int size, string value)
    {
        d.AsSpan(o, size).Clear();
        var bytes = Encoding.Latin1.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, size - 1)).CopyTo(d.AsSpan(o));
    }
}
