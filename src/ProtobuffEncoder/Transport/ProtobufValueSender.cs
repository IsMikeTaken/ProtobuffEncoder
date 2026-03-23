namespace ProtobuffEncoder.Transport;

/// <summary>
/// Sends single values and dynamic messages over a stream using length-delimited
/// protobuf framing. Works with strings, booleans, numbers, dates, and
/// <see cref="ProtoMessage"/> instances — no <c>[ProtoContract]</c> class required.
///
/// String values use the configured <see cref="ProtoEncoding"/> and fully support
/// emoji and all Unicode characters when using a Unicode-capable encoding.
/// </summary>
public sealed class ProtobufValueSender : IAsyncDisposable, IDisposable
{
    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private readonly ProtoEncoding _encoding;

    public ProtobufValueSender(Stream stream, ProtoEncoding? encoding = null, bool ownsStream = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _ownsStream = ownsStream;
        _encoding = encoding ?? ProtoEncoding.UTF8;
    }

    /// <summary>
    /// Sends a string value. Supports full Unicode including emoji.
    /// </summary>
    public void Send(string value) => SendBytes(ProtoValue.Encode(value, _encoding));

    /// <summary>
    /// Sends a string value asynchronously.
    /// </summary>
    public Task SendAsync(string value, CancellationToken cancellationToken = default)
        => SendBytesAsync(ProtoValue.Encode(value, _encoding), cancellationToken);

    /// <summary>Sends a boolean value.</summary>
    public void Send(bool value) => SendBytes(ProtoValue.Encode(value));

    /// <summary>Sends a boolean value asynchronously.</summary>
    public Task SendAsync(bool value, CancellationToken cancellationToken = default)
        => SendBytesAsync(ProtoValue.Encode(value), cancellationToken);

    /// <summary>Sends an int32 value.</summary>
    public void Send(int value) => SendBytes(ProtoValue.Encode(value));

    /// <summary>Sends an int32 value asynchronously.</summary>
    public Task SendAsync(int value, CancellationToken cancellationToken = default)
        => SendBytesAsync(ProtoValue.Encode(value), cancellationToken);

    /// <summary>Sends a long value.</summary>
    public void Send(long value) => SendBytes(ProtoValue.Encode(value));

    /// <summary>Sends a long value asynchronously.</summary>
    public Task SendAsync(long value, CancellationToken cancellationToken = default)
        => SendBytesAsync(ProtoValue.Encode(value), cancellationToken);

    /// <summary>Sends a double value.</summary>
    public void Send(double value) => SendBytes(ProtoValue.Encode(value));

    /// <summary>Sends a double value asynchronously.</summary>
    public Task SendAsync(double value, CancellationToken cancellationToken = default)
        => SendBytesAsync(ProtoValue.Encode(value), cancellationToken);

    /// <summary>Sends a float value.</summary>
    public void Send(float value) => SendBytes(ProtoValue.Encode(value));

    /// <summary>Sends a float value asynchronously.</summary>
    public Task SendAsync(float value, CancellationToken cancellationToken = default)
        => SendBytesAsync(ProtoValue.Encode(value), cancellationToken);

    /// <summary>Sends a DateTime value.</summary>
    public void Send(DateTime value) => SendBytes(ProtoValue.Encode(value));

    /// <summary>Sends a DateTime value asynchronously.</summary>
    public Task SendAsync(DateTime value, CancellationToken cancellationToken = default)
        => SendBytesAsync(ProtoValue.Encode(value), cancellationToken);

    /// <summary>Sends a Guid value.</summary>
    public void Send(Guid value) => SendBytes(ProtoValue.Encode(value));

    /// <summary>Sends a Guid value asynchronously.</summary>
    public Task SendAsync(Guid value, CancellationToken cancellationToken = default)
        => SendBytesAsync(ProtoValue.Encode(value), cancellationToken);

    /// <summary>Sends a byte array.</summary>
    public void Send(byte[] value) => SendBytes(ProtoValue.Encode(value));

    /// <summary>Sends a byte array asynchronously.</summary>
    public Task SendAsync(byte[] value, CancellationToken cancellationToken = default)
        => SendBytesAsync(ProtoValue.Encode(value), cancellationToken);

    /// <summary>Sends a dynamic <see cref="ProtoMessage"/>.</summary>
    public void Send(ProtoMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        message.WriteDelimitedTo(_stream);
        _stream.Flush();
    }

    /// <summary>Sends a dynamic <see cref="ProtoMessage"/> asynchronously.</summary>
    public async Task SendAsync(ProtoMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        await message.WriteDelimitedToAsync(_stream, cancellationToken);
    }

    /// <summary>Sends multiple string values from an async stream.</summary>
    public async Task SendManyAsync(IAsyncEnumerable<string> values, CancellationToken cancellationToken = default)
    {
        await foreach (var value in values.WithCancellation(cancellationToken))
            await SendAsync(value, cancellationToken);
    }

    /// <summary>Sends multiple messages from an async stream.</summary>
    public async Task SendManyAsync(IAsyncEnumerable<ProtoMessage> messages, CancellationToken cancellationToken = default)
    {
        await foreach (var message in messages.WithCancellation(cancellationToken))
            await SendAsync(message, cancellationToken);
    }

    private void SendBytes(byte[] payload)
    {
        WriteVarint(_stream, (ulong)payload.Length);
        _stream.Write(payload);
        _stream.Flush();
    }

    private async Task SendBytesAsync(byte[] payload, CancellationToken cancellationToken)
    {
        using var lengthBuf = new MemoryStream();
        WriteVarint(lengthBuf, (ulong)payload.Length);
        await _stream.WriteAsync(lengthBuf.ToArray(), cancellationToken);
        await _stream.WriteAsync(payload, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
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

    public void Dispose()
    {
        if (_ownsStream) _stream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsStream) await _stream.DisposeAsync();
    }
}
