namespace ProtobuffEncoder.Transport;

/// <summary>
/// Sends protobuf-encoded messages over a stream using length-delimited framing.
/// </summary>
public sealed class ProtobufSender<T> : IAsyncDisposable, IDisposable where T : class, new()
{
    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private readonly StaticMessage<T> _message = ProtobufEncoder.CreateStaticMessage<T>();

    public ProtobufSender(Stream stream, bool ownsStream = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _ownsStream = ownsStream;
    }

    /// <summary>
    /// Sends a single message synchronously.
    /// </summary>
    public void Send(T instance)
    {
        _message.WriteDelimited(instance, _stream);
        _stream.Flush();
    }

    /// <summary>
    /// Sends a single message asynchronously.
    /// </summary>
    public async Task SendAsync(T instance, CancellationToken cancellationToken = default)
    {
        await _message.WriteDelimitedAsync(instance, _stream, cancellationToken);
    }

    /// <summary>
    /// Sends multiple messages.
    /// </summary>
    public async Task SendManyAsync(IEnumerable<T> instances, CancellationToken cancellationToken = default)
    {
        foreach (var instance in instances)
        {
            await _message.WriteDelimitedAsync(instance, _stream, cancellationToken);
        }
    }

    /// <summary>
    /// Sends multiple messages from an async stream.
    /// </summary>
    public async Task SendManyAsync(IAsyncEnumerable<T> instances, CancellationToken cancellationToken = default)
    {
        await foreach (var instance in instances.WithCancellation(cancellationToken))
        {
            await _message.WriteDelimitedAsync(instance, _stream, cancellationToken);
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
