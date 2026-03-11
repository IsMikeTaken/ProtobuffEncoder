using System.Runtime.CompilerServices;

namespace ProtobuffEncoder.Transport;

/// <summary>
/// Bi-directional streaming channel over a single stream (or a pair of streams).
/// Supports sending and receiving protobuf messages simultaneously with length-delimited framing.
/// <para>
/// Use with pipes, TCP sockets, or any duplex stream where both sides can
/// send and receive concurrently.
/// </para>
/// </summary>
public sealed class ProtobufDuplexStream<TSend, TReceive> : IAsyncDisposable, IDisposable
    where TSend : class, new()
    where TReceive : class, new()
{
    private readonly Stream _sendStream;
    private readonly Stream _receiveStream;
    private readonly bool _ownsStreams;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _receiveLock = new(1, 1);

    /// <summary>
    /// Creates a duplex stream over a single bi-directional stream (e.g. NetworkStream, pipe).
    /// </summary>
    public ProtobufDuplexStream(Stream duplexStream, bool ownsStream = true)
        : this(duplexStream, duplexStream, ownsStream)
    {
    }

    /// <summary>
    /// Creates a duplex stream with separate send/receive streams.
    /// </summary>
    public ProtobufDuplexStream(Stream sendStream, Stream receiveStream, bool ownsStreams = true)
    {
        ArgumentNullException.ThrowIfNull(sendStream);
        ArgumentNullException.ThrowIfNull(receiveStream);
        _sendStream = sendStream;
        _receiveStream = receiveStream;
        _ownsStreams = ownsStreams;
    }

    #region Send

    /// <summary>
    /// Sends a single message. Thread-safe via internal lock.
    /// </summary>
    public async Task SendAsync(TSend message, CancellationToken cancellationToken = default)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await ProtobufEncoder.WriteDelimitedMessageAsync(message, _sendStream, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Sends a single message synchronously. Thread-safe via internal lock.
    /// </summary>
    public void Send(TSend message)
    {
        _sendLock.Wait();
        try
        {
            ProtobufEncoder.WriteDelimitedMessage(message, _sendStream);
            _sendStream.Flush();
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Sends multiple messages from an async stream.
    /// </summary>
    public async Task SendManyAsync(IAsyncEnumerable<TSend> messages, CancellationToken cancellationToken = default)
    {
        await foreach (var message in messages.WithCancellation(cancellationToken))
        {
            await SendAsync(message, cancellationToken);
        }
    }

    /// <summary>
    /// Sends multiple messages from an enumerable.
    /// </summary>
    public async Task SendManyAsync(IEnumerable<TSend> messages, CancellationToken cancellationToken = default)
    {
        foreach (var message in messages)
        {
            await SendAsync(message, cancellationToken);
        }
    }

    #endregion

    #region Receive

    /// <summary>
    /// Receives a single message. Returns null at end of stream. Thread-safe via internal lock.
    /// </summary>
    public async Task<TReceive?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        await _receiveLock.WaitAsync(cancellationToken);
        try
        {
            return await ReadOneAsync(_receiveStream, cancellationToken);
        }
        finally
        {
            _receiveLock.Release();
        }
    }

    /// <summary>
    /// Receives a single message synchronously. Returns null at end of stream.
    /// </summary>
    public TReceive? Receive()
    {
        _receiveLock.Wait();
        try
        {
            return ProtobufEncoder.ReadDelimitedMessage<TReceive>(_receiveStream);
        }
        finally
        {
            _receiveLock.Release();
        }
    }

    /// <summary>
    /// Receives messages as an async stream until end of stream or cancellation.
    /// </summary>
    public async IAsyncEnumerable<TReceive> ReceiveAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await ReceiveAsync(cancellationToken);
            if (message is null)
                yield break;
            yield return message;
        }
    }

    /// <summary>
    /// Listens for incoming messages and invokes a handler for each one.
    /// </summary>
    public async Task ListenAsync(Func<TReceive, Task> handler, CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReceiveAllAsync(cancellationToken))
        {
            await handler(message);
        }
    }

    #endregion

    #region Bi-directional patterns

    /// <summary>
    /// Request-response: sends a message and waits for a single response.
    /// Both operations are locked to ensure the response matches the request.
    /// </summary>
    public async Task<TReceive?> SendAndReceiveAsync(TSend request, CancellationToken cancellationToken = default)
    {
        await SendAsync(request, cancellationToken);
        return await ReceiveAsync(cancellationToken);
    }

    /// <summary>
    /// Runs send and receive concurrently. The sender pushes messages from
    /// <paramref name="outgoing"/> while the receiver invokes <paramref name="onReceived"/>
    /// for each incoming message. Completes when both sides finish.
    /// </summary>
    public async Task RunDuplexAsync(
        IAsyncEnumerable<TSend> outgoing,
        Func<TReceive, Task> onReceived,
        CancellationToken cancellationToken = default)
    {
        var sendTask = SendManyAsync(outgoing, cancellationToken);
        var receiveTask = ListenAsync(onReceived, cancellationToken);

        await Task.WhenAll(sendTask, receiveTask);
    }

    /// <summary>
    /// Processes a stream of requests, applying a transform function and sending each
    /// response back. Useful for server-side bidirectional streaming handlers.
    /// </summary>
    public async Task ProcessAsync(
        Func<TReceive, Task<TSend>> processor,
        CancellationToken cancellationToken = default)
    {
        await foreach (var request in ReceiveAllAsync(cancellationToken))
        {
            var response = await processor(request);
            await SendAsync(response, cancellationToken);
        }
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        _sendLock.Dispose();
        _receiveLock.Dispose();
        if (_ownsStreams)
        {
            _sendStream.Dispose();
            if (!ReferenceEquals(_sendStream, _receiveStream))
                _receiveStream.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _sendLock.Dispose();
        _receiveLock.Dispose();
        if (_ownsStreams)
        {
            await _sendStream.DisposeAsync();
            if (!ReferenceEquals(_sendStream, _receiveStream))
                await _receiveStream.DisposeAsync();
        }
    }

    #endregion

    #region Internal

    private static async Task<TReceive?> ReadOneAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Read varint length prefix
        ulong length = 0;
        int shift = 0;
        while (true)
        {
            var buf = new byte[1];
            int read = await stream.ReadAsync(buf, cancellationToken);
            if (read == 0)
                return shift == 0 ? null : throw new InvalidOperationException("Unexpected end of stream in varint.");

            byte b = buf[0];
            length |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                break;
            shift += 7;
        }

        var buffer = new byte[(int)length];
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0) throw new InvalidOperationException("Unexpected end of stream.");
            totalRead += read;
        }

        return ProtobufEncoder.Decode<TReceive>(buffer);
    }

    #endregion
}

/// <summary>
/// Convenience alias for a duplex stream where both sides use the same message type.
/// </summary>
public sealed class ProtobufDuplexStream<T> : IAsyncDisposable, IDisposable
    where T : class, new()
{
    private readonly ProtobufDuplexStream<T, T> _inner;

    public ProtobufDuplexStream(Stream duplexStream, bool ownsStream = true)
    {
        _inner = new ProtobufDuplexStream<T, T>(duplexStream, ownsStream);
    }

    public ProtobufDuplexStream(Stream sendStream, Stream receiveStream, bool ownsStreams = true)
    {
        _inner = new ProtobufDuplexStream<T, T>(sendStream, receiveStream, ownsStreams);
    }

    public Task SendAsync(T message, CancellationToken cancellationToken = default)
        => _inner.SendAsync(message, cancellationToken);

    public void Send(T message) => _inner.Send(message);

    public Task SendManyAsync(IAsyncEnumerable<T> messages, CancellationToken cancellationToken = default)
        => _inner.SendManyAsync(messages, cancellationToken);

    public Task SendManyAsync(IEnumerable<T> messages, CancellationToken cancellationToken = default)
        => _inner.SendManyAsync(messages, cancellationToken);

    public Task<T?> ReceiveAsync(CancellationToken cancellationToken = default)
        => _inner.ReceiveAsync(cancellationToken);

    public T? Receive() => _inner.Receive();

    public IAsyncEnumerable<T> ReceiveAllAsync(CancellationToken cancellationToken = default)
        => _inner.ReceiveAllAsync(cancellationToken);

    public Task ListenAsync(Func<T, Task> handler, CancellationToken cancellationToken = default)
        => _inner.ListenAsync(handler, cancellationToken);

    public Task<T?> SendAndReceiveAsync(T request, CancellationToken cancellationToken = default)
        => _inner.SendAndReceiveAsync(request, cancellationToken);

    public Task RunDuplexAsync(IAsyncEnumerable<T> outgoing, Func<T, Task> onReceived, CancellationToken cancellationToken = default)
        => _inner.RunDuplexAsync(outgoing, onReceived, cancellationToken);

    public Task ProcessAsync(Func<T, Task<T>> processor, CancellationToken cancellationToken = default)
        => _inner.ProcessAsync(processor, cancellationToken);

    public void Dispose() => _inner.Dispose();
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
