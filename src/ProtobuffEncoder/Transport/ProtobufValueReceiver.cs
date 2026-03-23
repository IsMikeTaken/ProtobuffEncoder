using System.Runtime.CompilerServices;

namespace ProtobuffEncoder.Transport;

/// <summary>
/// Receives single values and dynamic messages from a stream using length-delimited
/// protobuf framing. Decodes strings, booleans, numbers, dates, and
/// <see cref="ProtoMessage"/> instances — no <c>[ProtoContract]</c> class required.
///
/// String values use the configured <see cref="ProtoEncoding"/> and fully support
/// emoji and all Unicode characters when using a Unicode-capable encoding.
/// </summary>
public sealed class ProtobufValueReceiver : IAsyncDisposable, IDisposable
{
    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private readonly ProtoEncoding _encoding;

    public ProtobufValueReceiver(Stream stream, ProtoEncoding? encoding = null, bool ownsStream = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _ownsStream = ownsStream;
        _encoding = encoding ?? ProtoEncoding.UTF8;
    }

    /// <summary>
    /// Receives a string value. Returns null at end of stream.
    /// Supports full Unicode including emoji with Unicode-capable encodings.
    /// </summary>
    public string? ReceiveString()
    {
        var data = ReadPayload();
        return data is null ? null : ProtoValue.DecodeString(data, _encoding);
    }

    /// <summary>Receives a boolean value. Returns null at end of stream.</summary>
    public bool? ReceiveBool()
    {
        var data = ReadPayload();
        return data is null ? null : ProtoValue.DecodeBool(data);
    }

    /// <summary>Receives an int32 value. Returns null at end of stream.</summary>
    public int? ReceiveInt32()
    {
        var data = ReadPayload();
        return data is null ? null : ProtoValue.DecodeInt32(data);
    }

    /// <summary>Receives a long value. Returns null at end of stream.</summary>
    public long? ReceiveInt64()
    {
        var data = ReadPayload();
        return data is null ? null : ProtoValue.DecodeInt64(data);
    }

    /// <summary>Receives a double value. Returns null at end of stream.</summary>
    public double? ReceiveDouble()
    {
        var data = ReadPayload();
        return data is null ? null : ProtoValue.DecodeDouble(data);
    }

    /// <summary>Receives a float value. Returns null at end of stream.</summary>
    public float? ReceiveFloat()
    {
        var data = ReadPayload();
        return data is null ? null : ProtoValue.DecodeFloat(data);
    }

    /// <summary>Receives a DateTime value. Returns null at end of stream.</summary>
    public DateTime? ReceiveDateTime()
    {
        var data = ReadPayload();
        return data is null ? null : ProtoValue.DecodeDateTime(data);
    }

    /// <summary>Receives a Guid value. Returns null at end of stream.</summary>
    public Guid? ReceiveGuid()
    {
        var data = ReadPayload();
        return data is null ? null : ProtoValue.DecodeGuid(data);
    }

    /// <summary>Receives a byte array. Returns null at end of stream.</summary>
    public byte[]? ReceiveBytes()
    {
        var data = ReadPayload();
        return data is null ? null : ProtoValue.DecodeBytes(data);
    }

    /// <summary>
    /// Receives a dynamic <see cref="ProtoMessage"/>. Returns null at end of stream.
    /// </summary>
    public ProtoMessage? ReceiveMessage()
    {
        return ProtoMessage.ReadDelimitedFrom(_stream, _encoding);
    }

    /// <summary>Reads all string values until end of stream.</summary>
    public IEnumerable<string> ReceiveAllStrings()
    {
        while (true)
        {
            var value = ReceiveString();
            if (value is null) yield break;
            yield return value;
        }
    }

    /// <summary>Asynchronously reads all string values until end of stream.</summary>
    public async IAsyncEnumerable<string> ReceiveAllStringsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var data = await ReadPayloadAsync(cancellationToken);
            if (data is null) yield break;
            yield return ProtoValue.DecodeString(data, _encoding);
        }
    }

    /// <summary>Reads all dynamic messages until end of stream.</summary>
    public IEnumerable<ProtoMessage> ReceiveAllMessages()
    {
        return ProtoMessage.ReadAllDelimitedFrom(_stream, _encoding);
    }

    /// <summary>Asynchronously reads all dynamic messages until end of stream.</summary>
    public IAsyncEnumerable<ProtoMessage> ReceiveAllMessagesAsync(CancellationToken cancellationToken = default)
    {
        return ProtoMessage.ReadAllDelimitedFromAsync(_stream, _encoding, cancellationToken);
    }

    /// <summary>Invokes a callback for each received string until end of stream.</summary>
    public async Task ListenAsync(Func<string, Task> handler, CancellationToken cancellationToken = default)
    {
        await foreach (var value in ReceiveAllStringsAsync(cancellationToken))
            await handler(value);
    }

    /// <summary>Invokes a callback for each received string until end of stream.</summary>
    public async Task ListenAsync(Action<string> handler, CancellationToken cancellationToken = default)
    {
        await foreach (var value in ReceiveAllStringsAsync(cancellationToken))
            handler(value);
    }

    /// <summary>Invokes a callback for each received message until end of stream.</summary>
    public async Task ListenAsync(Func<ProtoMessage, Task> handler, CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReceiveAllMessagesAsync(cancellationToken))
            await handler(message);
    }

    private byte[]? ReadPayload()
    {
        if (!TryReadVarint(out ulong length))
            return null;

        var buffer = new byte[(int)length];
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = _stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0) throw new InvalidOperationException("Unexpected end of stream.");
            totalRead += read;
        }
        return buffer;
    }

    private async Task<byte[]?> ReadPayloadAsync(CancellationToken cancellationToken)
    {
        if (!TryReadVarint(out ulong length))
            return null;

        var buffer = new byte[(int)length];
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await _stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0) throw new InvalidOperationException("Unexpected end of stream.");
            totalRead += read;
        }
        return buffer;
    }

    private bool TryReadVarint(out ulong value)
    {
        value = 0;
        int shift = 0;
        while (true)
        {
            int byteRead = _stream.ReadByte();
            if (byteRead == -1)
                return shift == 0 ? false : throw new InvalidOperationException("Unexpected end of stream in varint.");

            byte b = (byte)byteRead;
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return true;
            shift += 7;
        }
    }

    public void Dispose()
    {
        if (_ownsStream) _stream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsStream) await _stream.DisposeAsync();
    }
}
