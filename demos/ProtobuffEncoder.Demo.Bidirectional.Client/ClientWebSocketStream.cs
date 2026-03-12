using System.Buffers;
using System.Net.WebSockets;

/// <summary>
/// Adapts a ClientWebSocket to a Stream for use with ProtobufDuplexStream.
/// </summary>
sealed class ClientWebSocketStream : Stream
{
    private readonly ClientWebSocket _ws;
    private readonly MemoryStream _receiveBuffer = new();
    private bool _receiveComplete;

    public ClientWebSocketStream(ClientWebSocket ws) => _ws = ws;

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_receiveBuffer.Position < _receiveBuffer.Length)
            return _receiveBuffer.Read(buffer.Span);

        if (_receiveComplete) return 0;

        _receiveBuffer.SetLength(0);
        _receiveBuffer.Position = 0;

        ValueWebSocketReceiveResult result;
        // Renting to avoid allocations
        using var rentedBuffer = MemoryPool<byte>.Shared.Rent(4096);

        try
        {
            do
            {
                result = await _ws.ReceiveAsync(rentedBuffer.Memory, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _receiveComplete = true;
                    return 0;
                }

                _receiveBuffer.Write(rentedBuffer.Memory.Span[..result.Count]);

            } while (!result.EndOfMessage);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely || ex.HResult == unchecked((int)0x80004005))
        {
            _receiveComplete = true;
            return 0;
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation so the caller knows we stopped because they asked
            throw;
        }
        catch (Exception)
        {
            // For any other fatal error, mark complete and re-throw
            _receiveComplete = true;
            throw;
        }

        _receiveBuffer.Position = 0;
        return _receiveBuffer.Read(buffer.Span);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _ws.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use ReadAsync");
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Use WriteAsync");
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _receiveBuffer.Dispose();
        base.Dispose(disposing);
    }
}