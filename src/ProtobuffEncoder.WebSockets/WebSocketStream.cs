using System.Buffers;
using System.Net.WebSockets;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// Adapts any <see cref="WebSocket"/> (server-side or <see cref="System.Net.WebSockets.ClientWebSocket"/>)
/// to a <see cref="Stream"/> for use with <see cref="Transport.ProtobufDuplexStream{TSend, TReceive}"/>.
/// Handles frame reassembly, graceful close detection, and buffer pooling.
/// </summary>
public sealed class WebSocketStream : Stream
{
    private readonly WebSocket _ws;
    private readonly MemoryStream _receiveBuffer = new();
    private bool _receiveComplete;

    public WebSocketStream(WebSocket ws)
    {
        ArgumentNullException.ThrowIfNull(ws);
        _ws = ws;
    }

    /// <summary>The underlying WebSocket instance.</summary>
    public WebSocket WebSocket => _ws;

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_receiveBuffer.Position < _receiveBuffer.Length)
            return _receiveBuffer.Read(buffer.Span);

        if (_receiveComplete) return 0;

        _receiveBuffer.SetLength(0);
        _receiveBuffer.Position = 0;

        using var rented = MemoryPool<byte>.Shared.Rent(4096);

        try
        {
            ValueWebSocketReceiveResult result;
            do
            {
                result = await _ws.ReceiveAsync(rented.Memory, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _receiveComplete = true;
                    return 0;
                }

                _receiveBuffer.Write(rented.Memory.Span[..result.Count]);
            } while (!result.EndOfMessage);
        }
        catch (WebSocketException ex)
            when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely
                  || ex.HResult == unchecked((int)0x80004005))
        {
            _receiveComplete = true;
            return 0;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            _receiveComplete = true;
            throw;
        }

        _receiveBuffer.Position = 0;
        return _receiveBuffer.Read(buffer.Span);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => await _ws.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => await WriteAsync(buffer.AsMemory(offset, count), cancellationToken);

    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use ReadAsync");
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use WriteAsync");
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _receiveBuffer.Dispose();
            if (_ws.State == WebSocketState.Open)
                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None)
                    .GetAwaiter().GetResult();
        }
        base.Dispose(disposing);
    }
}
