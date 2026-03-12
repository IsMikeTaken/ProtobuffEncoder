using System.Text;

namespace ProtobuffEncoder.Schema;

/// <summary>
/// Low-level protobuf writer for building messages without C# types.
/// Useful when the receiver only has .proto schemas and no compiled contract classes.
/// Supports scalar fields, nested messages, repeated fields, map fields, and packed encoding.
/// </summary>
public sealed class ProtobufWriter
{
    private readonly MemoryStream _stream = new();

    public void WriteVarint(int fieldNumber, long value)
    {
        WriteTag(fieldNumber, WireType.Varint);
        WriteRawVarint((ulong)value);
    }

    public void WriteBool(int fieldNumber, bool value)
    {
        WriteTag(fieldNumber, WireType.Varint);
        _stream.WriteByte(value ? (byte)1 : (byte)0);
    }

    public void WriteString(int fieldNumber, string value)
    {
        WriteTag(fieldNumber, WireType.LengthDelimited);
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteRawVarint((ulong)bytes.Length);
        _stream.Write(bytes);
    }

    public void WriteDouble(int fieldNumber, double value)
    {
        WriteTag(fieldNumber, WireType.Fixed64);
        _stream.Write(BitConverter.GetBytes(value));
    }

    public void WriteFloat(int fieldNumber, float value)
    {
        WriteTag(fieldNumber, WireType.Fixed32);
        _stream.Write(BitConverter.GetBytes(value));
    }

    public void WriteFixed64(int fieldNumber, long value)
    {
        WriteTag(fieldNumber, WireType.Fixed64);
        _stream.Write(BitConverter.GetBytes(value));
    }

    public void WriteBytes(int fieldNumber, byte[] value)
    {
        WriteTag(fieldNumber, WireType.LengthDelimited);
        WriteRawVarint((ulong)value.Length);
        _stream.Write(value);
    }

    /// <summary>
    /// Writes a nested message built by another <see cref="ProtobufWriter"/>.
    /// </summary>
    public void WriteMessage(int fieldNumber, ProtobufWriter nested)
    {
        var payload = nested.ToByteArray();
        WriteTag(fieldNumber, WireType.LengthDelimited);
        WriteRawVarint((ulong)payload.Length);
        _stream.Write(payload);
    }

    /// <summary>
    /// Writes a packed repeated field of varints.
    /// </summary>
    public void WritePackedVarints(int fieldNumber, IEnumerable<long> values)
    {
        using var packed = new MemoryStream();
        foreach (var v in values)
            WriteRawVarint(packed, (ulong)v);

        WriteTag(fieldNumber, WireType.LengthDelimited);
        WriteRawVarint((ulong)packed.Length);
        packed.Position = 0;
        packed.CopyTo(_stream);
    }

    /// <summary>
    /// Writes repeated strings (one tag per element).
    /// </summary>
    public void WriteRepeatedString(int fieldNumber, IEnumerable<string> values)
    {
        foreach (var v in values)
            WriteString(fieldNumber, v);
    }

    /// <summary>
    /// Writes repeated nested messages (one tag per element).
    /// </summary>
    public void WriteRepeatedMessage(int fieldNumber, IEnumerable<ProtobufWriter> messages)
    {
        foreach (var m in messages)
            WriteMessage(fieldNumber, m);
    }

    /// <summary>
    /// Writes a single map entry (key=1, value=2 inside a length-delimited wrapper).
    /// Call once per key-value pair. Proto: <c>map&lt;K, V&gt; field = N;</c>
    /// </summary>
    public void WriteMapEntry(int fieldNumber, Action<ProtobufWriter> writeKey, Action<ProtobufWriter> writeValue)
    {
        var entry = new ProtobufWriter();
        writeKey(entry);
        writeValue(entry);
        WriteMessage(fieldNumber, entry);
    }

    /// <summary>
    /// Writes a map&lt;string, string&gt; field.
    /// </summary>
    public void WriteStringStringMap(int fieldNumber, IEnumerable<KeyValuePair<string, string>> entries)
    {
        foreach (var kv in entries)
        {
            WriteMapEntry(fieldNumber,
                e => e.WriteString(1, kv.Key),
                e => e.WriteString(2, kv.Value));
        }
    }

    /// <summary>
    /// Writes a map&lt;string, int64&gt; field.
    /// </summary>
    public void WriteStringInt64Map(int fieldNumber, IEnumerable<KeyValuePair<string, long>> entries)
    {
        foreach (var kv in entries)
        {
            WriteMapEntry(fieldNumber,
                e => e.WriteString(1, kv.Key),
                e => e.WriteVarint(2, kv.Value));
        }
    }

    /// <summary>
    /// Writes a map&lt;string, message&gt; field where values are nested writers.
    /// </summary>
    public void WriteStringMessageMap(int fieldNumber, IEnumerable<KeyValuePair<string, ProtobufWriter>> entries)
    {
        foreach (var kv in entries)
        {
            WriteMapEntry(fieldNumber,
                e => e.WriteString(1, kv.Key),
                e => e.WriteMessage(2, kv.Value));
        }
    }

    /// <summary>
    /// Writes a map&lt;int32, string&gt; field.
    /// </summary>
    public void WriteIntStringMap(int fieldNumber, IEnumerable<KeyValuePair<int, string>> entries)
    {
        foreach (var kv in entries)
        {
            WriteMapEntry(fieldNumber,
                e => e.WriteVarint(1, kv.Key),
                e => e.WriteString(2, kv.Value));
        }
    }

    public byte[] ToByteArray()
    {
        return _stream.ToArray();
    }

    private void WriteTag(int fieldNumber, WireType wireType)
    {
        WriteRawVarint((ulong)((fieldNumber << 3) | (int)wireType));
    }

    private void WriteRawVarint(ulong value)
    {
        WriteRawVarint(_stream, value);
    }

    private static void WriteRawVarint(Stream stream, ulong value)
    {
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value > 0) b |= 0x80;
            stream.WriteByte(b);
        } while (value > 0);
    }
}
