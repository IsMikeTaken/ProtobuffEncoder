using System.Net.WebSockets;

sealed class WebSocketStream : Stream
{
    private readonly WebSocket _ws;
    private readonly MemoryStream _receiveBuffer = new();
    private bool _receiveComplete;

    public WebSocketStream(WebSocket ws) => _ws = ws;

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_receiveBuffer.Position < _receiveBuffer.Length)
            return _receiveBuffer.Read(buffer, offset, count);
        if (_receiveComplete) return 0;

        _receiveBuffer.SetLength(0);
        _receiveBuffer.Position = 0;
        var segment = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(new ArraySegment<byte>(segment), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) { _receiveComplete = true; return 0; }
            _receiveBuffer.Write(segment, 0, result.Count);
        } while (!result.EndOfMessage);

        _receiveBuffer.Position = 0;
        return _receiveBuffer.Read(buffer, offset, count);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_receiveBuffer.Position < _receiveBuffer.Length)
            return _receiveBuffer.Read(buffer.Span);
        if (_receiveComplete) return 0;

        _receiveBuffer.SetLength(0);
        _receiveBuffer.Position = 0;
        var segment = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(new ArraySegment<byte>(segment), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) { _receiveComplete = true; return 0; }
            _receiveBuffer.Write(segment, 0, result.Count);
        } while (!result.EndOfMessage);

        _receiveBuffer.Position = 0;
        return _receiveBuffer.Read(buffer.Span);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _ws.SendAsync(new ArraySegment<byte>(buffer, offset, count),
                            WebSocketMessageType.Binary, true, cancellationToken);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _ws.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
    }

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