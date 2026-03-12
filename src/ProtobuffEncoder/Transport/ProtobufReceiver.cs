using System.Runtime.CompilerServices;

namespace ProtobuffEncoder.Transport;

/// <summary>
/// Receives protobuf-encoded messages from a stream using length-delimited framing.
/// </summary>
public sealed class ProtobufReceiver<T> : IAsyncDisposable, IDisposable where T : class, new()
{
    private readonly Stream _stream;
    private readonly bool _ownsStream;

    public ProtobufReceiver(Stream stream, bool ownsStream = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _ownsStream = ownsStream;
    }

    /// <summary>
    /// Reads a single message synchronously. Returns null at end of stream.
    /// </summary>
    public T? Receive()
    {
        return ProtobufEncoder.ReadDelimitedMessage<T>(_stream);
    }

    /// <summary>
    /// Reads all messages synchronously until end of stream.
    /// </summary>
    public IEnumerable<T> ReceiveAll()
    {
        return ProtobufEncoder.ReadDelimitedMessages<T>(_stream);
    }

    /// <summary>
    /// Reads all messages asynchronously until end of stream or cancellation.
    /// </summary>
    public async IAsyncEnumerable<T> ReceiveAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in ProtobufEncoder.ReadDelimitedMessagesAsync<T>(_stream, cancellationToken))
        {
            yield return message;
        }
    }

    /// <summary>
    /// Invokes a callback for each received message until end of stream.
    /// </summary>
    public async Task ListenAsync(Func<T, Task> handler, CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReceiveAllAsync(cancellationToken))
        {
            await handler(message);
        }
    }

    /// <summary>
    /// Invokes a synchronous callback for each received message until end of stream.
    /// </summary>
    public async Task ListenAsync(Action<T> handler, CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReceiveAllAsync(cancellationToken))
        {
            handler(message);
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
