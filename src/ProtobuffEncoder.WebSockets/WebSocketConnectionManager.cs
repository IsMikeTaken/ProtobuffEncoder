using System.Collections.Concurrent;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// Thread-safe tracker of all active WebSocket connections for a given endpoint type pair.
/// Supports broadcast to all connected clients and filtered broadcast.
/// </summary>
public sealed class WebSocketConnectionManager<TSend, TReceive>
    where TSend : class, new()
    where TReceive : class, new()
{
    private readonly ConcurrentDictionary<string, ProtobufWebSocketConnection<TSend, TReceive>> _connections = new();

    /// <summary>Number of active connections.</summary>
    public int Count => _connections.Count;

    /// <summary>Snapshot of all active connections.</summary>
    public IReadOnlyCollection<ProtobufWebSocketConnection<TSend, TReceive>> Connections
        => _connections.Values.ToList().AsReadOnly();

    /// <summary>Gets a connection by ID. Returns null if not found.</summary>
    public ProtobufWebSocketConnection<TSend, TReceive>? GetConnection(string connectionId)
        => _connections.GetValueOrDefault(connectionId);

    /// <summary>
    /// Broadcasts a message to all connected clients. Thread-safe.
    /// Failed sends are caught and those connections are removed.
    /// </summary>
    public async Task BroadcastAsync(TSend message, CancellationToken cancellationToken = default)
    {
        await BroadcastAsync(message, _ => true, cancellationToken);
    }

    /// <summary>
    /// Broadcasts a message to connections matching the predicate (e.g., exclude the sender).
    /// </summary>
    public async Task BroadcastAsync(
        TSend message,
        Func<ProtobufWebSocketConnection<TSend, TReceive>, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        var snapshot = _connections.Values.Where(predicate).ToList();

        var tasks = snapshot.Select(async conn =>
        {
            try
            {
                if (conn.IsConnected)
                    await conn.SendAsync(message, cancellationToken);
            }
            catch
            {
                Remove(conn.ConnectionId);
            }
        });

        await Task.WhenAll(tasks);
    }

    internal void Add(ProtobufWebSocketConnection<TSend, TReceive> connection)
        => _connections.TryAdd(connection.ConnectionId, connection);

    internal void Remove(string connectionId)
        => _connections.TryRemove(connectionId, out _);
}
