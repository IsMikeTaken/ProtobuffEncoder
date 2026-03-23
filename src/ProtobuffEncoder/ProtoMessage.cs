using System.Collections;
using System.Globalization;
using System.Text;

namespace ProtobuffEncoder;

/// <summary>
/// A dynamic, schema-less protobuf message that uses field numbers and common CLR types
/// without requiring a <c>[ProtoContract]</c> class. Values are stored by field number and
/// automatically converted to/from protobuf binary format.
///
/// Supports all common types: strings (with configurable encoding and full emoji support),
/// booleans, integers, floating-point numbers, dates, GUIDs, byte arrays, and nested
/// <see cref="ProtoMessage"/> instances.
/// </summary>
public sealed class ProtoMessage
{
    private readonly Dictionary<int, FieldEntry> _fields = new();
    private readonly ProtoEncoding _encoding;

    /// <summary>
    /// Creates a new empty message with the specified default encoding for string fields.
    /// </summary>
    public ProtoMessage(ProtoEncoding? defaultEncoding = null)
    {
        _encoding = defaultEncoding ?? ProtoEncoding.UTF8;
    }

    /// <summary>
    /// The number of fields set on this message.
    /// </summary>
    public int FieldCount => _fields.Count;

    /// <summary>
    /// The field numbers that have been set.
    /// </summary>
    public IReadOnlyCollection<int> FieldNumbers => _fields.Keys;

    /// <summary>
    /// The default encoding used for string fields.
    /// </summary>
    public ProtoEncoding Encoding => _encoding;

    #region Set methods

    /// <summary>Sets a string field. Supports full Unicode including emoji.</summary>
    public ProtoMessage Set(int fieldNumber, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _fields[fieldNumber] = new FieldEntry(value, WireType.LengthDelimited);
        return this;
    }

    /// <summary>Sets a boolean field.</summary>
    public ProtoMessage Set(int fieldNumber, bool value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.Varint);
        return this;
    }

    /// <summary>Sets an int32 field.</summary>
    public ProtoMessage Set(int fieldNumber, int value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.Varint);
        return this;
    }

    /// <summary>Sets a uint32 field.</summary>
    public ProtoMessage Set(int fieldNumber, uint value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.Varint);
        return this;
    }

    /// <summary>Sets an int64 field.</summary>
    public ProtoMessage Set(int fieldNumber, long value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.Fixed64);
        return this;
    }

    /// <summary>Sets a uint64 field.</summary>
    public ProtoMessage Set(int fieldNumber, ulong value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.Fixed64);
        return this;
    }

    /// <summary>Sets a float field.</summary>
    public ProtoMessage Set(int fieldNumber, float value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.Fixed32);
        return this;
    }

    /// <summary>Sets a double field.</summary>
    public ProtoMessage Set(int fieldNumber, double value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.Fixed64);
        return this;
    }

    /// <summary>Sets a decimal field (stored as string representation).</summary>
    public ProtoMessage Set(int fieldNumber, decimal value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.LengthDelimited);
        return this;
    }

    /// <summary>Sets a DateTime field.</summary>
    public ProtoMessage Set(int fieldNumber, DateTime value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.Fixed64);
        return this;
    }

    /// <summary>Sets a DateTimeOffset field.</summary>
    public ProtoMessage Set(int fieldNumber, DateTimeOffset value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.LengthDelimited);
        return this;
    }

    /// <summary>Sets a TimeSpan field.</summary>
    public ProtoMessage Set(int fieldNumber, TimeSpan value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.Fixed64);
        return this;
    }

    /// <summary>Sets a DateOnly field.</summary>
    public ProtoMessage Set(int fieldNumber, DateOnly value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.LengthDelimited);
        return this;
    }

    /// <summary>Sets a TimeOnly field.</summary>
    public ProtoMessage Set(int fieldNumber, TimeOnly value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.LengthDelimited);
        return this;
    }

    /// <summary>Sets a Guid field.</summary>
    public ProtoMessage Set(int fieldNumber, Guid value)
    {
        _fields[fieldNumber] = new FieldEntry(value, WireType.LengthDelimited);
        return this;
    }

    /// <summary>Sets a byte array field.</summary>
    public ProtoMessage Set(int fieldNumber, byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _fields[fieldNumber] = new FieldEntry(value, WireType.LengthDelimited);
        return this;
    }

    /// <summary>Sets a nested message field.</summary>
    public ProtoMessage Set(int fieldNumber, ProtoMessage value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _fields[fieldNumber] = new FieldEntry(value, WireType.LengthDelimited);
        return this;
    }

    #endregion

    #region Get methods

    /// <summary>Gets a typed value by field number. Returns default if not found.</summary>
    public T? Get<T>(int fieldNumber)
    {
        if (!_fields.TryGetValue(fieldNumber, out var entry))
            return default;

        if (entry.Value is T typed)
            return typed;

        // Attempt conversion for numeric types
        try
        {
            return (T)Convert.ChangeType(entry.Value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>Gets a string value by field number.</summary>
    public string? GetString(int fieldNumber) => Get<string>(fieldNumber);

    /// <summary>Gets an int32 value by field number.</summary>
    public int GetInt32(int fieldNumber) => Get<int>(fieldNumber);

    /// <summary>Gets a long value by field number.</summary>
    public long GetInt64(int fieldNumber) => Get<long>(fieldNumber);

    /// <summary>Gets a boolean value by field number.</summary>
    public bool GetBool(int fieldNumber) => Get<bool>(fieldNumber);

    /// <summary>Gets a double value by field number.</summary>
    public double GetDouble(int fieldNumber) => Get<double>(fieldNumber);

    /// <summary>Gets a float value by field number.</summary>
    public float GetFloat(int fieldNumber) => Get<float>(fieldNumber);

    /// <summary>Gets a DateTime value by field number.</summary>
    public DateTime GetDateTime(int fieldNumber) => Get<DateTime>(fieldNumber);

    /// <summary>Gets a Guid value by field number.</summary>
    public Guid GetGuid(int fieldNumber) => Get<Guid>(fieldNumber);

    /// <summary>Gets a nested message by field number.</summary>
    public ProtoMessage? GetMessage(int fieldNumber) => Get<ProtoMessage>(fieldNumber);

    /// <summary>Checks whether a field has been set.</summary>
    public bool HasField(int fieldNumber) => _fields.ContainsKey(fieldNumber);

    /// <summary>Removes a field by number.</summary>
    public bool Remove(int fieldNumber) => _fields.Remove(fieldNumber);

    /// <summary>Gets the raw value of a field, or null if not set.</summary>
    public object? GetRaw(int fieldNumber) =>
        _fields.TryGetValue(fieldNumber, out var entry) ? entry.Value : null;

    #endregion

    #region Encode

    /// <summary>
    /// Encodes this message to protobuf binary format.
    /// </summary>
    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        WriteTo(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Writes this message to a stream in protobuf binary format.
    /// </summary>
    public void WriteTo(Stream output)
    {
        foreach (var (fieldNumber, entry) in _fields.OrderBy(kv => kv.Key))
        {
            uint tag = (uint)((fieldNumber << 3) | (int)entry.WireType);
            WriteVarint(output, tag);
            WriteValue(output, entry);
        }
    }

    /// <summary>
    /// Writes this message to a stream with length-delimited framing for transport.
    /// </summary>
    public void WriteDelimitedTo(Stream output)
    {
        var payload = ToBytes();
        WriteVarint(output, (ulong)payload.Length);
        output.Write(payload);
    }

    /// <summary>
    /// Asynchronously writes this message to a stream with length-delimited framing.
    /// </summary>
    public async Task WriteDelimitedToAsync(Stream output, CancellationToken cancellationToken = default)
    {
        var payload = ToBytes();
        using var lengthBuf = new MemoryStream();
        WriteVarint(lengthBuf, (ulong)payload.Length);
        await output.WriteAsync(lengthBuf.ToArray(), cancellationToken);
        await output.WriteAsync(payload, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private void WriteValue(Stream output, FieldEntry entry)
    {
        switch (entry.Value)
        {
            case bool b:
                WriteVarint(output, b ? 1UL : 0UL);
                break;
            case int i:
                WriteVarint(output, (ulong)(long)i);
                break;
            case uint u:
                WriteVarint(output, u);
                break;
            case long l:
                output.Write(BitConverter.GetBytes(l));
                break;
            case ulong ul:
                output.Write(BitConverter.GetBytes(ul));
                break;
            case float f:
                output.Write(BitConverter.GetBytes(f));
                break;
            case double d:
                output.Write(BitConverter.GetBytes(d));
                break;
            case string s:
                WriteLengthPrefixed(output, _encoding.GetBytes(s));
                break;
            case byte[] bytes:
                WriteLengthPrefixed(output, bytes);
                break;
            case decimal dec:
                WriteLengthPrefixed(output, _encoding.GetBytes(dec.ToString(CultureInfo.InvariantCulture)));
                break;
            case DateTime dt:
                output.Write(BitConverter.GetBytes(dt.Ticks));
                break;
            case DateTimeOffset dto:
                var dtoBytes = new byte[16];
                BitConverter.TryWriteBytes(dtoBytes.AsSpan(0, 8), dto.Ticks);
                BitConverter.TryWriteBytes(dtoBytes.AsSpan(8, 8), dto.Offset.Ticks);
                WriteLengthPrefixed(output, dtoBytes);
                break;
            case TimeSpan ts:
                output.Write(BitConverter.GetBytes(ts.Ticks));
                break;
            case DateOnly donly:
                WriteLengthPrefixed(output, BitConverter.GetBytes(donly.DayNumber));
                break;
            case TimeOnly tonly:
                WriteLengthPrefixed(output, BitConverter.GetBytes(tonly.Ticks));
                break;
            case Guid g:
                WriteLengthPrefixed(output, g.ToByteArray());
                break;
            case ProtoMessage nested:
                WriteLengthPrefixed(output, nested.ToBytes());
                break;
            default:
                throw new NotSupportedException($"ProtoMessage does not support type {entry.Value.GetType().Name}.");
        }
    }

    #endregion

    #region Decode

    /// <summary>
    /// Decodes a protobuf binary message into a <see cref="ProtoMessage"/>.
    /// String fields are decoded using the specified encoding (defaults to UTF-8).
    /// </summary>
    public static ProtoMessage FromBytes(ReadOnlySpan<byte> data, ProtoEncoding? encoding = null)
    {
        var enc = encoding ?? ProtoEncoding.UTF8;
        var message = new ProtoMessage(enc);

        int offset = 0;
        while (offset < data.Length)
        {
            uint tag = (uint)ReadVarint(data, ref offset);
            int fieldNumber = (int)(tag >> 3);
            var wireType = (WireType)(tag & 0x07);

            switch (wireType)
            {
                case WireType.Varint:
                    message._fields[fieldNumber] = new FieldEntry(ReadVarint(data, ref offset), WireType.Varint);
                    break;
                case WireType.Fixed32:
                    message._fields[fieldNumber] = new FieldEntry(
                        BitConverter.ToSingle(data.Slice(offset, 4)), WireType.Fixed32);
                    offset += 4;
                    break;
                case WireType.Fixed64:
                    message._fields[fieldNumber] = new FieldEntry(
                        BitConverter.ToInt64(data.Slice(offset, 8)), WireType.Fixed64);
                    offset += 8;
                    break;
                case WireType.LengthDelimited:
                    int length = (int)ReadVarint(data, ref offset);
                    var payload = data.Slice(offset, length);
                    offset += length;
                    // Store as string by default; callers can use GetRaw + reinterpret
                    message._fields[fieldNumber] = new FieldEntry(
                        enc.GetString(payload), WireType.LengthDelimited);
                    break;
                default:
                    throw new NotSupportedException($"Wire type {wireType} is not supported.");
            }
        }

        return message;
    }

    /// <summary>
    /// Reads a length-delimited <see cref="ProtoMessage"/> from a stream.
    /// Returns null at end of stream.
    /// </summary>
    public static ProtoMessage? ReadDelimitedFrom(Stream input, ProtoEncoding? encoding = null)
    {
        if (!TryReadVarintFromStream(input, out ulong length))
            return null;

        var buffer = new byte[(int)length];
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = input.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0) throw new InvalidOperationException("Unexpected end of stream.");
            totalRead += read;
        }

        return FromBytes(buffer, encoding);
    }

    /// <summary>
    /// Reads all length-delimited messages from a stream until EOF.
    /// </summary>
    public static IEnumerable<ProtoMessage> ReadAllDelimitedFrom(Stream input, ProtoEncoding? encoding = null)
    {
        while (true)
        {
            var message = ReadDelimitedFrom(input, encoding);
            if (message is null) yield break;
            yield return message;
        }
    }

    /// <summary>
    /// Asynchronously reads all length-delimited messages from a stream.
    /// </summary>
    public static async IAsyncEnumerable<ProtoMessage> ReadAllDelimitedFromAsync(
        Stream input,
        ProtoEncoding? encoding = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!TryReadVarintFromStream(input, out ulong length))
                yield break;

            var buffer = new byte[(int)length];
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = await input.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
                if (read == 0) throw new InvalidOperationException("Unexpected end of stream.");
                totalRead += read;
            }

            yield return FromBytes(buffer, encoding);
        }
    }

    #endregion

    #region Wire helpers

    private static void WriteLengthPrefixed(Stream output, byte[] data)
    {
        WriteVarint(output, (ulong)data.Length);
        output.Write(data);
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

    private static bool TryReadVarintFromStream(Stream stream, out ulong value)
    {
        value = 0;
        int shift = 0;
        while (true)
        {
            int byteRead = stream.ReadByte();
            if (byteRead == -1)
                return shift == 0 ? false : throw new InvalidOperationException("Unexpected end of stream in varint.");

            byte b = (byte)byteRead;
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return true;
            shift += 7;
        }
    }

    #endregion

    private readonly record struct FieldEntry(object Value, WireType WireType);
}
