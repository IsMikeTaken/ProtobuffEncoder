using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace ProtobuffEncoder.WebSockets.Tests.Fixtures;

/// <summary>
/// In-memory WebSocket implementation for unit testing.
/// Supports queued receive messages, state tracking, and error injection.
/// Used across multiple test classes for Mock-Object and Service-Simulation patterns.
/// </summary>
public sealed class FakeWebSocket : WebSocket
{
    private WebSocketState _state = WebSocketState.Open;
    private readonly ConcurrentQueue<(byte[] Data, WebSocketMessageType Type, bool EndOfMessage)> _receiveQueue = new();
    private readonly ConcurrentQueue<byte[]> _sentMessages = new();
    private WebSocketCloseStatus? _closeStatus;
    private string? _closeDescription;
    private Exception? _receiveException;
    private int _receiveDelayMs;

    public override WebSocketCloseStatus? CloseStatus => _closeStatus;
    public override string? CloseStatusDescription => _closeDescription;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;

    /// <summary>All messages sent through SendAsync, captured for assertion.</summary>
    public IReadOnlyCollection<byte[]> SentMessages => _sentMessages.ToArray();

    /// <summary>Number of SendAsync calls made.</summary>
    public int SendCount => _sentMessages.Count;

    /// <summary>Enqueue a binary message to be returned on the next ReceiveAsync call.</summary>
    public void EnqueueReceive(byte[] data, bool endOfMessage = true)
        => _receiveQueue.Enqueue((data, WebSocketMessageType.Binary, endOfMessage));

    /// <summary>Enqueue a protobuf-encoded message for ReceiveAsync.</summary>
    public void EnqueueMessage<T>(T message) where T : class
        => EnqueueReceive(ProtobufEncoder.Encode(message));

    /// <summary>Enqueue a close frame.</summary>
    public void EnqueueClose()
        => _receiveQueue.Enqueue((Array.Empty<byte>(), WebSocketMessageType.Close, true));

    /// <summary>Inject an exception to be thrown on the next ReceiveAsync call.</summary>
    public void InjectReceiveError(Exception ex) => _receiveException = ex;

    /// <summary>Add artificial delay to ReceiveAsync calls (ms).</summary>
    public void SetReceiveDelay(int delayMs) => _receiveDelayMs = delayMs;

    /// <summary>Manually set the WebSocket state.</summary>
    public void SetState(WebSocketState state) => _state = state;

    public override async Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        if (_receiveDelayMs > 0)
            await Task.Delay(_receiveDelayMs, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (_receiveException is not null)
        {
            var ex = _receiveException;
            _receiveException = null;
            throw ex;
        }

        if (_receiveQueue.TryDequeue(out var item))
        {
            if (item.Type == WebSocketMessageType.Close)
            {
                _state = WebSocketState.CloseReceived;
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
                    WebSocketCloseStatus.NormalClosure, "Done");
            }

            Array.Copy(item.Data, 0, buffer.Array!, buffer.Offset,
                Math.Min(item.Data.Length, buffer.Count));
            return new WebSocketReceiveResult(
                Math.Min(item.Data.Length, buffer.Count), item.Type, item.EndOfMessage);
        }

        // No more data — simulate close
        _state = WebSocketState.CloseReceived;
        return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true,
            WebSocketCloseStatus.NormalClosure, "Done");
    }

    public override async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (_receiveDelayMs > 0)
            await Task.Delay(_receiveDelayMs, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (_receiveException is not null)
        {
            var ex = _receiveException;
            _receiveException = null;
            throw ex;
        }

        if (_receiveQueue.TryDequeue(out var item))
        {
            if (item.Type == WebSocketMessageType.Close)
            {
                _state = WebSocketState.CloseReceived;
                return new ValueWebSocketReceiveResult(
                    0, WebSocketMessageType.Close, true);
            }

            var count = Math.Min(item.Data.Length, buffer.Length);
            item.Data.AsMemory(0, count).CopyTo(buffer);
            return new ValueWebSocketReceiveResult(
                count, item.Type, item.EndOfMessage);
        }

        _state = WebSocketState.CloseReceived;
        return new ValueWebSocketReceiveResult(
            0, WebSocketMessageType.Close, true);
    }

    public override Task SendAsync(ArraySegment<byte> buffer,
        WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_state != WebSocketState.Open)
            throw new WebSocketException(WebSocketError.InvalidState, "WebSocket is not open");

        var copy = new byte[buffer.Count];
        Array.Copy(buffer.Array!, buffer.Offset, copy, 0, buffer.Count);
        _sentMessages.Enqueue(copy);
        return Task.CompletedTask;
    }

    public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer,
        WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_state != WebSocketState.Open)
            throw new WebSocketException(WebSocketError.InvalidState, "WebSocket is not open");

        _sentMessages.Enqueue(buffer.ToArray());
        return ValueTask.CompletedTask;
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus,
        string? statusDescription, CancellationToken cancellationToken)
    {
        _closeStatus = closeStatus;
        _closeDescription = statusDescription;
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus,
        string? statusDescription, CancellationToken cancellationToken)
    {
        _closeStatus = closeStatus;
        _closeDescription = statusDescription;
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }

    public override void Abort()
    {
        _state = WebSocketState.Aborted;
    }

    public override void Dispose() { }
}
