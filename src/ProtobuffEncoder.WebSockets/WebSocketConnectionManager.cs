using System.Collections.Concurrent;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// Thread-safe registry of all active WebSocket connections for a given
/// <typeparamref name="TSend"/> / <typeparamref name="TReceive"/> endpoint type pair.
/// </summary>
/// <remarks>
/// <para>
/// Connections are stored in a <see cref="ConcurrentDictionary{TKey,TValue}"/> so
/// <see cref="Add"/> and <see cref="Remove"/> are lock-free.
/// </para>
/// <para>
/// <see cref="BroadcastAsync(TSend,CancellationToken)"/> uses
/// <c>Parallel.ForEachAsync</c> to fan out sends concurrently on
/// the thread-pool without the LINQ-allocation overhead of
/// <c>Task.WhenAll(snapshot.Select(…))</c>.
/// Failed sends are silently removed from the registry.
/// </para>
/// </remarks>
/// <typeparam name="TSend">The message type sent to clients.</typeparam>
/// <typeparam name="TReceive">The message type received from clients.</typeparam>
public sealed class WebSocketConnectionManager<TSend, TReceive>
    where TSend : class, new()
    where TReceive : class, new()
{
    private readonly ConcurrentDictionary<string, ProtobufWebSocketConnection<TSend, TReceive>>
        _connections = new();

    /// <summary>Number of currently active connections.</summary>
    public int Count => _connections.Count;

    /// <summary>
    /// A point-in-time snapshot of all active connections.
    /// The returned list is safe to iterate after the call returns.
    /// </summary>
    public IReadOnlyList<ProtobufWebSocketConnection<TSend, TReceive>> Connections
        => [.. _connections.Values];

    /// <summary>
    /// Returns the connection with the given <paramref name="connectionId"/>,
    /// or <see langword="null"/> if it is not present.
    /// </summary>
    /// <param name="connectionId">The connection identifier assigned at accept time.</param>
    public ProtobufWebSocketConnection<TSend, TReceive>? GetConnection(string connectionId)
        => _connections.GetValueOrDefault(connectionId);

    /// <summary>
    /// Broadcasts <paramref name="message"/> to every connected client concurrently.
    /// </summary>
    /// <remarks>
    /// Connections that throw during the send are silently removed from the registry.
    /// </remarks>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="cancellationToken">Propagated to each individual send.</param>
    public Task BroadcastAsync(TSend message, CancellationToken cancellationToken = default)
        => BroadcastAsync(message, static _ => true, cancellationToken);

    /// <summary>
    /// Broadcasts <paramref name="message"/> to all connections that satisfy
    /// <paramref name="predicate"/> (e.g. exclude the sender).
    /// </summary>
    /// <remarks>
    /// Uses <c>Parallel.ForEachAsync</c> for concurrent fan-out
    /// without allocating a LINQ projection or a <c>Task[]</c> array.
    /// Failed sends are silently removed from the registry.
    /// </remarks>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="predicate">Filter applied to each connection before sending.</param>
    /// <param name="cancellationToken">Propagated to each individual send.</param>
    public Task BroadcastAsync(
        TSend message,
        Func<ProtobufWebSocketConnection<TSend, TReceive>, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        // Take a snapshot so additions/removals during broadcast don't affect iteration.
        var targets = _connections.Values
            .Where(c => c.IsConnected && predicate(c))
            .ToList();

        if (targets.Count == 0)
            return Task.CompletedTask;

        return Parallel.ForEachAsync(
            targets,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount,
            },
            async (conn, ct) =>
            {
                try
                {
                    await conn.SendDirectAsync(message, ct).ConfigureAwait(false);
                }
                catch
                {
                    Remove(conn.ConnectionId);
                }
            });
    }

    internal void Add(ProtobufWebSocketConnection<TSend, TReceive> connection)
        => _connections.TryAdd(connection.ConnectionId, connection);

    internal void Remove(string connectionId)
        => _connections.TryRemove(connectionId, out _);
}


