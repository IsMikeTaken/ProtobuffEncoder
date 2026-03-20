using System.Net.WebSockets;
using ProtobuffEncoder.WebSockets.Tests.Fixtures;

namespace ProtobuffEncoder.WebSockets.Tests;

/// <summary>
/// Tests for <see cref="WebSocketStream"/> — the Stream adapter that bridges
/// any WebSocket to the protobuf transport layer.
/// </summary>
public class WebSocketStreamTests
{
    #region Simple-Test Pattern — verifies the most basic behavior

    [Fact]
    public void Constructor_NullWebSocket_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new WebSocketStream(null!));
    }

    [Fact]
    public void StreamCapabilities_ReportsCorrectly()
    {
        var ws = new FakeWebSocket();
        using var stream = new WebSocketStream(ws);

        Assert.True(stream.CanRead);
        Assert.True(stream.CanWrite);
        Assert.False(stream.CanSeek);
    }

    [Fact]
    public void WebSocketProperty_ExposesUnderlyingSocket()
    {
        var ws = new FakeWebSocket();
        using var stream = new WebSocketStream(ws);

        Assert.Same(ws, stream.WebSocket);
    }

    #endregion

    #region Code-Path Pattern — exercises every reachable branch

    [Fact]
    public async Task ReadAsync_BinaryMessage_ReturnsData()
    {
        var ws = new FakeWebSocket();
        var payload = new byte[] { 0x08, 0x2A }; // varint tag=1, value=42
        ws.EnqueueReceive(payload);

        using var stream = new WebSocketStream(ws);
        var buffer = new byte[256];
        int read = await stream.ReadAsync(buffer.AsMemory());

        Assert.Equal(payload.Length, read);
        Assert.Equal(payload, buffer[..read]);
    }

    [Fact]
    public async Task ReadAsync_CloseMessage_ReturnsZero()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueClose();

        using var stream = new WebSocketStream(ws);
        var buffer = new byte[256];
        int read = await stream.ReadAsync(buffer.AsMemory());

        Assert.Equal(0, read);
    }

    [Fact]
    public async Task ReadAsync_AfterClose_SubsequentReadsReturnZero()
    {
        // Process-State Pattern: once closed, stays closed
        var ws = new FakeWebSocket();
        ws.EnqueueClose();

        using var stream = new WebSocketStream(ws);
        var buffer = new byte[256];

        int first = await stream.ReadAsync(buffer.AsMemory());
        int second = await stream.ReadAsync(buffer.AsMemory());

        Assert.Equal(0, first);
        Assert.Equal(0, second);
    }

    [Fact]
    public async Task ReadAsync_MultipleMessages_ReadsSequentially()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueReceive(new byte[] { 0x01 });
        ws.EnqueueReceive(new byte[] { 0x02, 0x03 });
        ws.EnqueueClose();

        using var stream = new WebSocketStream(ws);
        var buffer = new byte[256];

        int r1 = await stream.ReadAsync(buffer.AsMemory());
        Assert.Equal(1, r1);
        Assert.Equal(0x01, buffer[0]);

        int r2 = await stream.ReadAsync(buffer.AsMemory());
        Assert.Equal(2, r2);
        Assert.Equal(0x02, buffer[0]);
        Assert.Equal(0x03, buffer[1]);

        int r3 = await stream.ReadAsync(buffer.AsMemory());
        Assert.Equal(0, r3);
    }

    [Fact]
    public async Task WriteAsync_SendsBinaryFrame()
    {
        var ws = new FakeWebSocket();
        using var stream = new WebSocketStream(ws);

        var data = new byte[] { 0x0A, 0x0B, 0x0C };
        await stream.WriteAsync(data.AsMemory());

        Assert.Single(ws.SentMessages);
        Assert.Equal(data, ws.SentMessages.First());
    }

    [Fact]
    public async Task WriteAsync_ArrayOverload_SendsBinaryFrame()
    {
        var ws = new FakeWebSocket();
        using var stream = new WebSocketStream(ws);

        var data = new byte[] { 0x0A, 0x0B, 0x0C };
        await stream.WriteAsync(data, 0, data.Length, CancellationToken.None);

        Assert.Single(ws.SentMessages);
        Assert.Equal(data, ws.SentMessages.First());
    }

    [Fact]
    public async Task ReadAsync_ArrayOverload_ReturnsData()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueReceive(new byte[] { 0xAA, 0xBB });

        using var stream = new WebSocketStream(ws);
        var buffer = new byte[256];
        int read = await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);

        Assert.Equal(2, read);
        Assert.Equal(0xAA, buffer[0]);
        Assert.Equal(0xBB, buffer[1]);
    }

    #endregion

    #region Bit-Error-Simulation Pattern — corrupted/abnormal data

    [Fact]
    public async Task ReadAsync_PrematureClose_ReturnsZeroGracefully()
    {
        var ws = new FakeWebSocket();
        ws.InjectReceiveError(new WebSocketException(
            WebSocketError.ConnectionClosedPrematurely, "Connection reset"));

        using var stream = new WebSocketStream(ws);
        var buffer = new byte[256];
        int read = await stream.ReadAsync(buffer.AsMemory());

        Assert.Equal(0, read);
    }

    [Fact]
    public async Task ReadAsync_UnexpectedError_Throws()
    {
        var ws = new FakeWebSocket();
        ws.InjectReceiveError(new InvalidOperationException("Unexpected failure"));

        using var stream = new WebSocketStream(ws);
        var buffer = new byte[256];

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => stream.ReadAsync(buffer.AsMemory()).AsTask());
    }

    [Fact]
    public async Task ReadAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        var ws = new FakeWebSocket();
        ws.SetReceiveDelay(5000);

        using var stream = new WebSocketStream(ws);
        using var cts = new CancellationTokenSource(50);
        var buffer = new byte[256];

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => stream.ReadAsync(buffer.AsMemory(), cts.Token).AsTask());
    }

    #endregion

    #region Parameter-Range Pattern — edge cases on inputs

    [Fact]
    public void SyncRead_ThrowsNotSupported()
    {
        var ws = new FakeWebSocket();
        using var stream = new WebSocketStream(ws);

        Assert.Throws<NotSupportedException>(() => stream.Read(new byte[10], 0, 10));
    }

    [Fact]
    public void SyncWrite_ThrowsNotSupported()
    {
        var ws = new FakeWebSocket();
        using var stream = new WebSocketStream(ws);

        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[10], 0, 10));
    }

    [Fact]
    public void Seek_ThrowsNotSupported()
    {
        var ws = new FakeWebSocket();
        using var stream = new WebSocketStream(ws);

        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    [Fact]
    public void SetLength_ThrowsNotSupported()
    {
        var ws = new FakeWebSocket();
        using var stream = new WebSocketStream(ws);

        Assert.Throws<NotSupportedException>(() => stream.SetLength(100));
    }

    [Fact]
    public void Length_ThrowsNotSupported()
    {
        var ws = new FakeWebSocket();
        using var stream = new WebSocketStream(ws);

        Assert.Throws<NotSupportedException>(() => _ = stream.Length);
    }

    [Fact]
    public void Position_GetSet_ThrowsNotSupported()
    {
        var ws = new FakeWebSocket();
        using var stream = new WebSocketStream(ws);

        Assert.Throws<NotSupportedException>(() => _ = stream.Position);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
    }

    [Fact]
    public void Flush_DoesNotThrow()
    {
        var ws = new FakeWebSocket();
        using var stream = new WebSocketStream(ws);

        var ex = Record.Exception(() => stream.Flush());
        Assert.Null(ex);
    }

    [Fact]
    public async Task ReadAsync_EmptyMessage_ReturnsZeroBytes()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueReceive(Array.Empty<byte>());

        using var stream = new WebSocketStream(ws);
        var buffer = new byte[256];
        int read = await stream.ReadAsync(buffer.AsMemory());

        Assert.Equal(0, read);
    }

    #endregion

    #region Process-State Pattern — state transitions

    [Fact]
    public void Dispose_ClosesOpenWebSocket()
    {
        var ws = new FakeWebSocket();
        var stream = new WebSocketStream(ws);

        Assert.Equal(WebSocketState.Open, ws.State);

        stream.Dispose();

        Assert.Equal(WebSocketState.Closed, ws.State);
    }

    [Fact]
    public void Dispose_AlreadyClosed_DoesNotThrow()
    {
        var ws = new FakeWebSocket();
        ws.SetState(WebSocketState.Closed);

        var stream = new WebSocketStream(ws);
        var ex = Record.Exception(() => stream.Dispose());

        Assert.Null(ex);
    }

    #endregion

    #region Simple-Data-I/O Pattern — round-trip encode/decode through stream

    [Fact]
    public async Task RoundTrip_ProtobufMessage_ThroughStream()
    {
        var ws = new FakeWebSocket();
        var original = new Heartbeat { Timestamp = 1234567890L };
        var encoded = ProtobufEncoder.Encode(original);
        ws.EnqueueReceive(encoded);

        using var stream = new WebSocketStream(ws);

        // Write
        await stream.WriteAsync(encoded.AsMemory());

        // Read
        var buffer = new byte[4096];
        int read = await stream.ReadAsync(buffer.AsMemory());
        var decoded = ProtobufEncoder.Decode<Heartbeat>(buffer.AsSpan(0, read));

        Assert.Equal(original.Timestamp, decoded.Timestamp);
        Assert.Equal(encoded, ws.SentMessages.First());
    }

    #endregion

    #region Performance-Test Pattern — verify write throughput bounds

    [Fact]
    public async Task WriteAsync_HighVolume_CompletesWithinBound()
    {
        var ws = new FakeWebSocket();
        using var stream = new WebSocketStream(ws);
        var data = new byte[64];

        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int iterations = 10_000;

        for (int i = 0; i < iterations; i++)
            await stream.WriteAsync(data.AsMemory());

        sw.Stop();

        Assert.Equal(iterations, ws.SendCount);
        // 10k writes to an in-memory socket should complete well under 2 seconds
        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"Expected < 2000ms, took {sw.ElapsedMilliseconds}ms");
    }

    #endregion
}
