using System.Buffers;
using System.Net.WebSockets;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// Adapts any <see cref="WebSocket"/> to a <see cref="Stream"/> for use with
/// <see cref="Transport.ProtobufDuplexStream{TSend,TReceive}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Receive buffering</strong> — incoming WebSocket frames are reassembled
/// into a single contiguous message using a buffer rented from
/// <see cref="ArrayPool{T}.Shared"/> (default 64 KiB). The rental is returned on
/// every receive iteration so no heap memory is held between messages.
/// </para>
/// <para>
/// <strong>Send path</strong> — data is written directly to the WebSocket as a
/// single binary frame; no intermediate buffer is needed.
/// </para>
/// <para>
/// <strong>Disposal</strong> — both <see cref="DisposeAsync"/> and
/// <see cref="Dispose(bool)"/> send a graceful <c>NormalClosure</c> close frame if
/// the socket is still open. Prefer <c>await using</c> to avoid blocking the thread.
/// </para>
/// </remarks>
/// <example>
/// Server-side usage with graceful async disposal:
/// <code>
/// await using var stream = new WebSocketStream(webSocket);
/// var duplex = new ProtobufDuplexStream&lt;MyRequest, MyResponse&gt;(stream);
/// await foreach (var msg in duplex.ReceiveAllAsync(ct)) { … }
/// </code>
/// </example>
public sealed class WebSocketStream : Stream, IAsyncDisposable
{
    /// <summary>
    /// Default frame-reassembly buffer size (64 KiB).
    /// Large enough for typical protobuf messages; the buffer is rented per-message
    /// and returned immediately, so this value does not affect steady-state memory.
    /// </summary>
    public const int DefaultReceiveBufferSize = 64 * 1024;

    private readonly WebSocket _ws;
    private readonly int _receiveBufferSize;

    // Per-message reassembly state — reset at the start of each new message.
    private byte[]? _messageBuffer;  // grows as needed via ArrayPool re-rent
    private int _messageLength;  // bytes written into _messageBuffer
    private int _messagePosition;// bytes already consumed by Read callers
    private bool _receiveComplete;

    /// <summary>
    /// Initialises a new <see cref="WebSocketStream"/> wrapping <paramref name="ws"/>.
    /// </summary>
    /// <param name="ws">The WebSocket to adapt. Must not be <see langword="null"/>.</param>
    /// <param name="receiveBufferSize">
    /// Size of the per-frame receive rental from <see cref="ArrayPool{T}.Shared"/>.
    /// Defaults to <see cref="DefaultReceiveBufferSize"/> (64 KiB).
    /// </param>
    public WebSocketStream(WebSocket ws, int receiveBufferSize = DefaultReceiveBufferSize)
    {
        ArgumentNullException.ThrowIfNull(ws);
        _ws = ws;
        _receiveBufferSize = receiveBufferSize;
    }

    /// <summary>The underlying <see cref="WebSocket"/> instance.</summary>
    public WebSocket WebSocket => _ws;

    /// <summary>Always <see langword="true"/>; this stream supports reading.</summary>
    public override bool CanRead => true;

    /// <summary>Always <see langword="true"/>; this stream supports writing.</summary>
    public override bool CanWrite => true;

    /// <summary>Always <see langword="false"/>; WebSocket streams are not seekable.</summary>
    public override bool CanSeek => false;

    /// <summary>Not supported. Always throws <see cref="NotSupportedException"/>.</summary>
    public override long Length => throw new NotSupportedException();

    /// <summary>Not supported. Always throws <see cref="NotSupportedException"/>.</summary>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Reads the next protobuf message from the WebSocket into <paramref name="buffer"/>.
    /// Reassembles multi-frame messages transparently using a pooled receive buffer.
    /// Returns 0 at end-of-stream (graceful close or premature disconnect).
    /// </summary>
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Return unconsumed bytes from the previously received message.
        if (_messageBuffer is not null && _messagePosition < _messageLength)
        {
            int available = _messageLength - _messagePosition;
            int toCopy = Math.Min(available, buffer.Length);
            _messageBuffer.AsMemory(_messagePosition, toCopy).CopyTo(buffer);
            _messagePosition += toCopy;
            return toCopy;
        }

        if (_receiveComplete)
            return 0;

        // Reset reassembly state for the incoming message.
        ReturnMessageBuffer();
        _messageLength = 0;
        _messagePosition = 0;

        byte[] frameBuffer = ArrayPool<byte>.Shared.Rent(_receiveBufferSize);
        try
        {
            ValueWebSocketReceiveResult result;
            do
            {
                result = await _ws.ReceiveAsync(frameBuffer.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _receiveComplete = true;
                    return 0;
                }

                EnsureMessageCapacity(_messageLength + result.Count);
                frameBuffer.AsSpan(0, result.Count).CopyTo(_messageBuffer!.AsSpan(_messageLength));
                _messageLength += result.Count;
            }
            while (!result.EndOfMessage);
        }
        catch (WebSocketException ex)
            when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely
                  || ex.HResult == unchecked((int)0x80004005))
        {
            _receiveComplete = true;
            return 0;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            _receiveComplete = true;
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(frameBuffer);
        }

        int copy = Math.Min(_messageLength, buffer.Length);
        _messageBuffer!.AsMemory(0, copy).CopyTo(buffer);
        _messagePosition = copy;
        return copy;
    }

    /// <summary>
    /// Reads from the WebSocket into <paramref name="buffer"/>.
    /// Delegates to <see cref="ReadAsync(Memory{byte},CancellationToken)"/>.
    /// </summary>
    public override Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>
    /// Sends <paramref name="buffer"/> to the remote endpoint as a single binary WebSocket frame.
    /// </summary>
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _ws.SendAsync(buffer, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);

    /// <summary>
    /// Sends <paramref name="buffer"/> to the remote endpoint as a single binary WebSocket frame.
    /// Delegates to <see cref="WriteAsync(ReadOnlyMemory{byte},CancellationToken)"/>.
    /// </summary>
    public override Task WriteAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>No-op; WebSocket writes are flushed per-frame automatically.</summary>
    public override void Flush() { }

    /// <summary>Not supported. Use <see cref="ReadAsync(Memory{byte},CancellationToken)"/>.</summary>
    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException("Use ReadAsync.");

    /// <summary>Not supported. Use <see cref="WriteAsync(ReadOnlyMemory{byte},CancellationToken)"/>.</summary>
    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException("Use WriteAsync.");

    /// <summary>Not supported. WebSocket streams are not seekable.</summary>
    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    /// <summary>Not supported.</summary>
    public override void SetLength(long value)
        => throw new NotSupportedException();

    /// <summary>
    /// Sends a graceful <c>NormalClosure</c> close frame if the socket is open,
    /// then releases the pooled message buffer. Prefer over synchronous disposal.
    /// </summary>
    public new async ValueTask DisposeAsync()
    {
        try
        {
            if (_ws.State == WebSocketState.Open)
            {
                await _ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Done",
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch { /* best-effort close */ }
        finally
        {
            ReturnMessageBuffer();
            base.Dispose(true);
        }
    }

    /// <summary>
    /// Sends a synchronous graceful close (blocking) and releases the pooled buffer.
    /// Prefer <see cref="DisposeAsync"/> to avoid blocking the calling thread.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReturnMessageBuffer();
            if (_ws.State == WebSocketState.Open)
            {
                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None)
                   .GetAwaiter().GetResult();
            }
        }
        base.Dispose(disposing);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void EnsureMessageCapacity(int required)
    {
        if (_messageBuffer is null)
        {
            _messageBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(required, _receiveBufferSize));
            return;
        }

        if (_messageBuffer.Length >= required)
            return;

        // Grow: rent a larger buffer, copy existing data, return the old one.
        byte[] larger = ArrayPool<byte>.Shared.Rent(required * 2);
        _messageBuffer.AsSpan(0, _messageLength).CopyTo(larger);
        ArrayPool<byte>.Shared.Return(_messageBuffer);
        _messageBuffer = larger;
    }

    private void ReturnMessageBuffer()
    {
        if (_messageBuffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_messageBuffer);
            _messageBuffer = null;
        }
    }
}

