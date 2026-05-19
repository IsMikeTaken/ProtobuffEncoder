using System.Buffers;
using System.Net.WebSockets;
using ProtobuffEncoder.Transport;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// Represents a single active WebSocket connection with protobuf duplex streaming.
/// </summary>
/// <remarks>
/// <para>
/// Wraps a <see cref="WebSocketStream"/> and a <see cref="ProtobufDuplexStream{TSend,TReceive}"/>
/// and exposes connection metadata, lifecycle state, and thread-safe send/receive.
/// </para>
/// <para>
/// <strong>v2 send path</strong> — prefer <see cref="SendDirectAsync"/> over
/// <see cref="SendAsync"/> on hot paths.  <see cref="SendDirectAsync"/> encodes the
/// message directly into a pooled <see cref="ArrayBufferWriter{T}"/> and writes the
/// resulting buffer to the WebSocket in a single frame, eliminating the intermediate
/// <see cref="System.IO.MemoryStream"/> allocation used by the stream-based path.
/// </para>
/// </remarks>
/// <typeparam name="TSend">The message type sent to the client.</typeparam>
/// <typeparam name="TReceive">The message type received from the client.</typeparam>
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

    /// <summary>Unique identifier assigned at accept time.</summary>
    public string ConnectionId { get; }

    /// <summary>UTC timestamp when the connection was established.</summary>
    public DateTimeOffset ConnectedAt { get; }

    /// <summary>
    /// <see langword="true"/> while the underlying WebSocket state is
    /// <see cref="WebSocketState.Open"/>.
    /// </summary>
    public bool IsConnected => _wsStream.WebSocket.State == WebSocketState.Open;

    /// <summary>
    /// The underlying duplex stream for advanced patterns such as
    /// <c>RunDuplexAsync</c> and <c>ProcessAsync</c>.
    /// </summary>
    /// <remarks>
    /// Prefer the typed helpers (<see cref="SendAsync"/>, <see cref="ReceiveAsync"/>,
    /// <see cref="SendDirectAsync"/>) over direct stream access where possible.
    /// </remarks>
    public ProtobufDuplexStream<TSend, TReceive> Stream => _duplex;

    // ── Send ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends <paramref name="message"/> using the stream-based length-delimited path.
    /// </summary>
    /// <remarks>
    /// This method allocates an internal <see cref="System.IO.MemoryStream"/> for encoding.
    /// Prefer <see cref="SendDirectAsync"/> on throughput-sensitive paths — it encodes
    /// directly into a pooled <see cref="ArrayBufferWriter{T}"/> and writes a single
    /// WebSocket frame with no intermediate allocation.
    /// </remarks>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Propagated to the underlying WebSocket write.</param>
    [Obsolete("Use SendDirectAsync for lower allocation overhead. SendAsync will be removed in a future major version.")]
    public Task SendAsync(TSend message, CancellationToken cancellationToken = default)
        => _duplex.SendAsync(message, cancellationToken);

    /// <summary>
    /// Encodes <paramref name="message"/> into a pooled buffer and sends it as a single
    /// binary WebSocket frame — no intermediate <see cref="System.IO.MemoryStream"/> is allocated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses <see cref="ProtobufEncoder.EncodeTo"/> with an <see cref="ArrayBufferWriter{T}"/>
    /// rented from a shared pool, writes the encoded bytes directly to the WebSocket via
    /// <see cref="WebSocket.SendAsync(ReadOnlyMemory{byte},WebSocketMessageType,bool,CancellationToken)"/>,
    /// then returns the writer to the pool.
    /// </para>
    /// <para>
    /// Unlike <see cref="SendAsync"/>, this method bypasses the length-delimited framing
    /// layer — it writes exactly the raw protobuf bytes as a single WebSocket binary frame.
    /// Use it when the receiver also calls <see cref="ReceiveDirectAsync"/> (which expects
    /// a single-frame message without a length prefix).
    /// </para>
    /// </remarks>
    /// <param name="message">The message to encode and send.</param>
    /// <param name="cancellationToken">Propagated to the underlying WebSocket write.</param>
    public async ValueTask SendDirectAsync(TSend message, CancellationToken cancellationToken = default)
    {
        // Rent a buffer writer, encode into it, send the bytes, return the writer.
        var writer = new ArrayBufferWriter<byte>();
        ProtobufEncoder.EncodeTo(message, writer);

        await _wsStream.WebSocket.SendAsync(
            writer.WrittenMemory,
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cancellationToken).ConfigureAwait(false);
    }

    // ── Receive ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Receives a single length-delimited message. Returns <see langword="null"/> at
    /// end of stream.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="ReceiveDirectAsync"/> when pairing with <see cref="SendDirectAsync"/> —
    /// it reads a raw WebSocket frame without a length-prefix parse step.
    /// </remarks>
    /// <param name="cancellationToken">Propagated to the underlying WebSocket read.</param>
    [Obsolete("Use ReceiveDirectAsync when paired with SendDirectAsync. ReceiveAsync will be removed in a future major version.")]
    public Task<TReceive?> ReceiveAsync(CancellationToken cancellationToken = default)
        => _duplex.ReceiveAsync(cancellationToken);

    /// <summary>
    /// Reads a single raw binary WebSocket frame, decodes it as a protobuf message,
    /// and returns the result — no length-prefix overhead.
    /// </summary>
    /// <remarks>
    /// Pairs with <see cref="SendDirectAsync"/>: both sides must agree on the framing
    /// convention (raw frame vs. length-delimited stream).  Returns <see langword="null"/>
    /// when the WebSocket closes gracefully.
    /// </remarks>
    /// <param name="cancellationToken">Propagated to the underlying WebSocket read.</param>
    public async ValueTask<TReceive?> ReceiveDirectAsync(CancellationToken cancellationToken = default)
    {
        // Read a full WebSocket message (may span multiple frames) into a pooled buffer.
        byte[] frameBuffer = ArrayPool<byte>.Shared.Rent(WebSocketStream.DefaultReceiveBufferSize);
        try
        {
            int totalRead = 0;
            ValueWebSocketReceiveResult result;

            do
            {
                if (totalRead >= frameBuffer.Length)
                {
                    // Grow the buffer if the message is larger than the initial rental.
                    byte[] larger = ArrayPool<byte>.Shared.Rent(frameBuffer.Length * 2);
                    frameBuffer.AsSpan(0, totalRead).CopyTo(larger);
                    ArrayPool<byte>.Shared.Return(frameBuffer);
                    frameBuffer = larger;
                }

                result = await _wsStream.WebSocket
                    .ReceiveAsync(frameBuffer.AsMemory(totalRead), cancellationToken)
                    .ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                    return null;

                totalRead += result.Count;
            }
            while (!result.EndOfMessage);

            return ProtobufEncoder.Decode<TReceive>(frameBuffer.AsSpan(0, totalRead));
        }
        catch (WebSocketException ex)
            when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely
                  || ex.HResult == unchecked((int)0x80004005))
        {
            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(frameBuffer);
        }
    }

    /// <summary>
    /// Receives all length-delimited messages as an async stream until the connection closes
    /// or <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="ReceiveAllDirectAsync"/> when pairing with <see cref="SendDirectAsync"/>.
    /// </remarks>
    /// <param name="cancellationToken">Propagated to each receive operation.</param>
    [Obsolete("Use ReceiveAllDirectAsync when paired with SendDirectAsync. ReceiveAllAsync will be removed in a future major version.")]
    public IAsyncEnumerable<TReceive> ReceiveAllAsync(CancellationToken cancellationToken = default)
        => _duplex.ReceiveAllAsync(cancellationToken);

    /// <summary>
    /// Receives all raw-frame messages as an async stream until the connection closes
    /// or <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <remarks>
    /// Pairs with <see cref="SendDirectAsync"/>. Each iteration calls
    /// <see cref="ReceiveDirectAsync"/> once.
    /// </remarks>
    /// <param name="cancellationToken">Propagated to each receive operation.</param>
    public async IAsyncEnumerable<TReceive> ReceiveAllDirectAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TReceive? message = await ReceiveDirectAsync(cancellationToken).ConfigureAwait(false);
            if (message is null)
                yield break;
            yield return message;
        }
    }

    // ── Disposal ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Disposes the duplex stream and sends a graceful WebSocket close frame.
    /// </summary>
    public async ValueTask DisposeAsync()
        => await _duplex.DisposeAsync().ConfigureAwait(false);
}
