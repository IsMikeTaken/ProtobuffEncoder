using System.Net.WebSockets;
using ProtobuffEncoder.Transport;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// Represents a single WebSocket connection with protobuf duplex streaming.
/// Wraps a <see cref="ProtobufDuplexStream{TSend, TReceive}"/> over a <see cref="WebSocketStream"/>
/// and exposes connection metadata, lifecycle state, and thread-safe send/receive.
/// </summary>
public sealed class ProtobufWebSocketConnection<TSend, TReceive> : IAsyncDisposable
    where TSend : class, new()
    where TReceive : class, new()
{
    private readonly WebSocketStream _wsStream;
    private readonly ProtobufDuplexStream<TSend, TReceive> _duplex;

    internal ProtobufWebSocketConnection(WebSocket ws, string connectionId)
    {
        ConnectionId = connectionId;
        ConnectedAt = DateTimeOffset.UtcNow;
        _wsStream = new WebSocketStream(ws);
        _duplex = new ProtobufDuplexStream<TSend, TReceive>(_wsStream, ownsStream: true);
    }

    /// <summary>Unique identifier for this connection.</summary>
    public string ConnectionId { get; }

    /// <summary>When the connection was established.</summary>
    public DateTimeOffset ConnectedAt { get; }

    /// <summary>Whether the underlying WebSocket is still open.</summary>
    public bool IsConnected => _wsStream.WebSocket.State == WebSocketState.Open;

    /// <summary>The underlying duplex stream for advanced patterns (RunDuplexAsync, ProcessAsync, etc.).</summary>
    public ProtobufDuplexStream<TSend, TReceive> Stream => _duplex;

    /// <summary>Sends a single message. Thread-safe.</summary>
    public Task SendAsync(TSend message, CancellationToken cancellationToken = default)
        => _duplex.SendAsync(message, cancellationToken);

    /// <summary>Receives a single message. Returns null at end of stream.</summary>
    public Task<TReceive?> ReceiveAsync(CancellationToken cancellationToken = default)
        => _duplex.ReceiveAsync(cancellationToken);

    /// <summary>Receives all messages as an async stream until disconnect.</summary>
    public IAsyncEnumerable<TReceive> ReceiveAllAsync(CancellationToken cancellationToken = default)
        => _duplex.ReceiveAllAsync(cancellationToken);

    public async ValueTask DisposeAsync()
        => await _duplex.DisposeAsync();
}
