using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace ProtobuffEncoder;

/// <summary>
/// Encodes and decodes single primitive values to/from protobuf binary format
/// without requiring a <c>[ProtoContract]</c> class. Each value is written as a
/// single protobuf field (field number 1) so it can be used directly with
/// the transport layer.
///
/// Supports all common types: strings, booleans, integers, floating-point,
/// dates, GUIDs, and more. String values use the specified <see cref="ProtoEncoding"/>
/// (defaults to UTF-8) and fully support emoji and all Unicode characters.
/// </summary>
public static class ProtoValue
{
    private const int FieldNumber = 1;

    #region Encode

    /// <summary>
    /// Encodes a string value. Supports full Unicode including emoji when using
    /// a Unicode-capable encoding (UTF-8, UTF-16, UTF-32).
    /// </summary>
    public static byte[] Encode(string value, ProtoEncoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var stream = new MemoryStream();
        var enc = encoding ?? ProtoEncoding.UTF8;
        var payload = enc.GetBytes(value);
        WriteTag(stream, WireType.LengthDelimited);
        WriteVarint(stream, (ulong)payload.Length);
        stream.Write(payload);
        return stream.ToArray();
    }

    /// <summary>Encodes a boolean value.</summary>
    public static byte[] Encode(bool value)
    {
        using var stream = new MemoryStream();
        WriteTag(stream, WireType.Varint);
        WriteVarint(stream, value ? 1UL : 0UL);
        return stream.ToArray();
    }

    /// <summary>Encodes a 32-bit signed integer.</summary>
    public static byte[] Encode(int value)
    {
        using var stream = new MemoryStream();
        WriteTag(stream, WireType.Varint);
        WriteVarint(stream, (ulong)(long)value);
        return stream.ToArray();
    }

    /// <summary>Encodes a 32-bit unsigned integer.</summary>
    public static byte[] Encode(uint value)
    {
        using var stream = new MemoryStream();
        WriteTag(stream, WireType.Varint);
        WriteVarint(stream, value);
        return stream.ToArray();
    }

    /// <summary>Encodes a 64-bit signed integer.</summary>
    public static byte[] Encode(long value)
    {
        using var stream = new MemoryStream();
        WriteTag(stream, WireType.Fixed64);
        stream.Write(BitConverter.GetBytes(value));
        return stream.ToArray();
    }

    /// <summary>Encodes a 64-bit unsigned integer.</summary>
    public static byte[] Encode(ulong value)
    {
        using var stream = new MemoryStream();
        WriteTag(stream, WireType.Fixed64);
        stream.Write(BitConverter.GetBytes(value));
        return stream.ToArray();
    }

    /// <summary>Encodes a single-precision float.</summary>
    public static byte[] Encode(float value)
    {
        using var stream = new MemoryStream();
        WriteTag(stream, WireType.Fixed32);
        stream.Write(BitConverter.GetBytes(value));
        return stream.ToArray();
    }

    /// <summary>Encodes a double-precision float.</summary>
    public static byte[] Encode(double value)
    {
        using var stream = new MemoryStream();
        WriteTag(stream, WireType.Fixed64);
        stream.Write(BitConverter.GetBytes(value));
        return stream.ToArray();
    }

    /// <summary>Encodes a decimal as a UTF-8 string representation.</summary>
    public static byte[] Encode(decimal value)
    {
        return Encode(value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Encodes a DateTime as fixed64 ticks.</summary>
    public static byte[] Encode(DateTime value)
    {
        using var stream = new MemoryStream();
        WriteTag(stream, WireType.Fixed64);
        stream.Write(BitConverter.GetBytes(value.Ticks));
        return stream.ToArray();
    }

    /// <summary>Encodes a DateTimeOffset as a length-delimited pair of tick values.</summary>
    public static byte[] Encode(DateTimeOffset value)
    {
        using var stream = new MemoryStream();
        var payload = new byte[16];
        BitConverter.TryWriteBytes(payload.AsSpan(0, 8), value.Ticks);
        BitConverter.TryWriteBytes(payload.AsSpan(8, 8), value.Offset.Ticks);
        WriteTag(stream, WireType.LengthDelimited);
        WriteVarint(stream, (ulong)payload.Length);
        stream.Write(payload);
        return stream.ToArray();
    }

    /// <summary>Encodes a TimeSpan as fixed64 ticks.</summary>
    public static byte[] Encode(TimeSpan value)
    {
        using var stream = new MemoryStream();
        WriteTag(stream, WireType.Fixed64);
        stream.Write(BitConverter.GetBytes(value.Ticks));
        return stream.ToArray();
    }

    /// <summary>Encodes a DateOnly as its day number.</summary>
    public static byte[] Encode(DateOnly value)
    {
        using var stream = new MemoryStream();
        var payload = BitConverter.GetBytes(value.DayNumber);
        WriteTag(stream, WireType.LengthDelimited);
        WriteVarint(stream, (ulong)payload.Length);
        stream.Write(payload);
        return stream.ToArray();
    }

    /// <summary>Encodes a TimeOnly as its tick count.</summary>
    public static byte[] Encode(TimeOnly value)
    {
        using var stream = new MemoryStream();
        var payload = BitConverter.GetBytes(value.Ticks);
        WriteTag(stream, WireType.LengthDelimited);
        WriteVarint(stream, (ulong)payload.Length);
        stream.Write(payload);
        return stream.ToArray();
    }

    /// <summary>Encodes a GUID as 16 bytes.</summary>
    public static byte[] Encode(Guid value)
    {
        using var stream = new MemoryStream();
        var payload = value.ToByteArray();
        WriteTag(stream, WireType.LengthDelimited);
        WriteVarint(stream, (ulong)payload.Length);
        stream.Write(payload);
        return stream.ToArray();
    }

    /// <summary>Encodes a byte array as a length-delimited field.</summary>
    public static byte[] Encode(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var stream = new MemoryStream();
        WriteTag(stream, WireType.LengthDelimited);
        WriteVarint(stream, (ulong)value.Length);
        stream.Write(value);
        return stream.ToArray();
    }

    /// <summary>Encodes a short as varint.</summary>
    public static byte[] Encode(short value) => Encode((int)value);

    /// <summary>Encodes a byte as varint.</summary>
    public static byte[] Encode(byte value) => Encode((int)value);

    /// <summary>Encodes a Half as length-delimited.</summary>
    public static byte[] Encode(Half value)
    {
        using var stream = new MemoryStream();
        var payload = BitConverter.GetBytes(value);
        WriteTag(stream, WireType.LengthDelimited);
        WriteVarint(stream, (ulong)payload.Length);
        stream.Write(payload);
        return stream.ToArray();
    }

    #endregion

    #region Decode

    /// <summary>
    /// Decodes a string value. Uses the specified encoding (defaults to UTF-8).
    /// Fully supports emoji and all Unicode characters with Unicode-capable encodings.
    /// </summary>
    public static string DecodeString(ReadOnlySpan<byte> data, ProtoEncoding? encoding = null)
    {
        var enc = encoding ?? ProtoEncoding.UTF8;
        int offset = 0;
        ReadTag(data, ref offset);
        int length = (int)ReadVarint(data, ref offset);
        return enc.GetString(data.Slice(offset, length));
    }

    /// <summary>Decodes a boolean value.</summary>
    public static bool DecodeBool(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        return ReadVarint(data, ref offset) != 0;
    }

    /// <summary>Decodes a 32-bit signed integer.</summary>
    public static int DecodeInt32(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        return (int)(long)ReadVarint(data, ref offset);
    }

    /// <summary>Decodes a 32-bit unsigned integer.</summary>
    public static uint DecodeUInt32(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        return (uint)ReadVarint(data, ref offset);
    }

    /// <summary>Decodes a 64-bit signed integer.</summary>
    public static long DecodeInt64(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        return BitConverter.ToInt64(data.Slice(offset, 8));
    }

    /// <summary>Decodes a 64-bit unsigned integer.</summary>
    public static ulong DecodeUInt64(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        return BitConverter.ToUInt64(data.Slice(offset, 8));
    }

    /// <summary>Decodes a single-precision float.</summary>
    public static float DecodeFloat(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        return BitConverter.ToSingle(data.Slice(offset, 4));
    }

    /// <summary>Decodes a double-precision float.</summary>
    public static double DecodeDouble(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        return BitConverter.ToDouble(data.Slice(offset, 8));
    }

    /// <summary>Decodes a decimal from its string representation.</summary>
    public static decimal DecodeDecimal(ReadOnlySpan<byte> data, ProtoEncoding? encoding = null)
    {
        return decimal.Parse(DecodeString(data, encoding), CultureInfo.InvariantCulture);
    }

    /// <summary>Decodes a DateTime from fixed64 ticks.</summary>
    public static DateTime DecodeDateTime(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        return new DateTime(BitConverter.ToInt64(data.Slice(offset, 8)));
    }

    /// <summary>Decodes a DateTimeOffset from a length-delimited pair of tick values.</summary>
    public static DateTimeOffset DecodeDateTimeOffset(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        int length = (int)ReadVarint(data, ref offset);
        var payload = data.Slice(offset, length);
        long ticks = BitConverter.ToInt64(payload.Slice(0, 8));
        long offsetTicks = BitConverter.ToInt64(payload.Slice(8, 8));
        return new DateTimeOffset(ticks, new TimeSpan(offsetTicks));
    }

    /// <summary>Decodes a TimeSpan from fixed64 ticks.</summary>
    public static TimeSpan DecodeTimeSpan(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        return new TimeSpan(BitConverter.ToInt64(data.Slice(offset, 8)));
    }

    /// <summary>Decodes a DateOnly from its day number.</summary>
    public static DateOnly DecodeDateOnly(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        int length = (int)ReadVarint(data, ref offset);
        return DateOnly.FromDayNumber(BitConverter.ToInt32(data.Slice(offset, length)));
    }

    /// <summary>Decodes a TimeOnly from its tick count.</summary>
    public static TimeOnly DecodeTimeOnly(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        int length = (int)ReadVarint(data, ref offset);
        return new TimeOnly(BitConverter.ToInt64(data.Slice(offset, length)));
    }

    /// <summary>Decodes a GUID from 16 bytes.</summary>
    public static Guid DecodeGuid(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        int length = (int)ReadVarint(data, ref offset);
        return new Guid(data.Slice(offset, length));
    }

    /// <summary>Decodes a byte array.</summary>
    public static byte[] DecodeBytes(ReadOnlySpan<byte> data)
    {
        int offset = 0;
        ReadTag(data, ref offset);
        int length = (int)ReadVarint(data, ref offset);
        return data.Slice(offset, length).ToArray();
    }

    #endregion

    #region Wire helpers

    private static void WriteTag(Stream stream, WireType wireType)
    {
        uint tag = (uint)((FieldNumber << 3) | (int)wireType);
        WriteVarint(stream, tag);
    }

    private static void ReadTag(ReadOnlySpan<byte> data, ref int offset)
    {
        ReadVarint(data, ref offset); // consume the tag
    }

    private static void WriteVarint(Stream output, ulong value)
    {
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value > 0) b |= 0x80;
            output.WriteByte(b);
        } while (value > 0);
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int offset)
    {
        ulong result = 0;
        int shift = 0;
        while (offset < data.Length)
        {
            byte b = data[offset++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
        }
        throw new InvalidOperationException("Unexpected end of data while reading varint.");
    }

    #endregion
}
