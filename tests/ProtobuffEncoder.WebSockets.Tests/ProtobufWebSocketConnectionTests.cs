using System.Net.WebSockets;
using ProtobuffEncoder.WebSockets.Tests.Fixtures;

namespace ProtobuffEncoder.WebSockets.Tests;

/// <summary>
/// Tests for <see cref="ProtobufWebSocketConnection{TSend, TReceive}"/> — per-connection
/// wrapper with lifecycle state, send/receive, and async dispose.
/// </summary>
public class ProtobufWebSocketConnectionTests
{
    #region Simple-Test Pattern — construction and metadata

    [Fact]
    public void Constructor_SetsConnectionIdAndTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(
            new FakeWebSocket(), "test-123");
        var after = DateTimeOffset.UtcNow;

        Assert.Equal("test-123", conn.ConnectionId);
        Assert.InRange(conn.ConnectedAt, before, after);
    }

    [Fact]
    public void IsConnected_OpenSocket_ReturnsTrue()
    {
        var ws = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "c");

        Assert.True(conn.IsConnected);
    }

    [Fact]
    public void Stream_IsNotNull()
    {
        var ws = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "c");

        Assert.NotNull(conn.Stream);
    }

    #endregion

    #region Process-State Pattern — connection state transitions

    [Fact]
    public void IsConnected_ClosedSocket_ReturnsFalse()
    {
        var ws = new FakeWebSocket();
        ws.SetState(WebSocketState.Closed);

        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "c");

        Assert.False(conn.IsConnected);
    }

    [Fact]
    public void IsConnected_AbortedSocket_ReturnsFalse()
    {
        var ws = new FakeWebSocket();
        ws.SetState(WebSocketState.Aborted);

        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "c");

        Assert.False(conn.IsConnected);
    }

    [Fact]
    public async Task DisposeAsync_CompletesWithoutException()
    {
        var ws = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "c");

        var ex = await Record.ExceptionAsync(() => conn.DisposeAsync().AsTask());
        Assert.Null(ex);
    }

    #endregion

    #region Simple-Data-I/O Pattern — send/receive round-trip

    [Fact]
    public async Task SendAsync_EncodesAndWritesToSocket()
    {
        var ws = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "c");

        await conn.SendAsync(new Heartbeat { Timestamp = 42 });

        // WriteDelimitedMessage writes varint length prefix + payload as separate frames
        Assert.True(ws.SendCount >= 1, $"Expected at least 1 send, got {ws.SendCount}");
        Assert.True(ws.SentMessages.First().Length > 0);
    }

    [Fact]
    public async Task ReceiveAsync_DecodesFromSocket()
    {
        var ws = new FakeWebSocket();
        var original = new Heartbeat { Timestamp = 9999 };

        // Encode as length-delimited for the duplex stream protocol
        using var ms = new MemoryStream();
        ProtobufEncoder.WriteDelimitedMessage(original, ms);
        ws.EnqueueReceive(ms.ToArray());
        ws.EnqueueClose();

        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "c");
        var received = await conn.ReceiveAsync();

        Assert.NotNull(received);
        Assert.Equal(original.Timestamp, received!.Timestamp);
    }

    [Fact]
    public async Task ReceiveAsync_EndOfStream_ReturnsNull()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueClose();

        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "c");
        var result = await conn.ReceiveAsync();

        Assert.Null(result);
    }

    #endregion

    #region Data Transaction Pattern — send-then-receive sequence

    [Fact]
    public async Task SendAndReceive_DifferentTypes_WorksCorrectly()
    {
        var ws = new FakeWebSocket();

        // Prepare a ChatReply response to be received
        var reply = new ChatReply { From = "server", Body = "Hello!", Delivered = true };
        using var ms = new MemoryStream();
        ProtobufEncoder.WriteDelimitedMessage(reply, ms);
        ws.EnqueueReceive(ms.ToArray());
        ws.EnqueueClose();

        var conn = new ProtobufWebSocketConnection<ChatMessage, ChatReply>(ws, "chat-1");

        // Send a ChatMessage
        await conn.SendAsync(new ChatMessage { User = "alice", Text = "Hi" });
        Assert.True(ws.SendCount >= 1);

        // Receive a ChatReply
        var received = await conn.ReceiveAsync();
        Assert.NotNull(received);
        Assert.Equal("server", received!.From);
        Assert.Equal("Hello!", received.Body);
        Assert.True(received.Delivered);
    }

    #endregion

    #region Enumeration Pattern — ReceiveAllAsync async enumerable

    [Fact]
    public async Task ReceiveAllAsync_YieldsAllMessages()
    {
        var ws = new FakeWebSocket();

        // Enqueue 3 heartbeats as length-delimited
        using var ms = new MemoryStream();
        for (int i = 1; i <= 3; i++)
            ProtobufEncoder.WriteDelimitedMessage(new Heartbeat { Timestamp = i * 100 }, ms);
        ws.EnqueueReceive(ms.ToArray());
        ws.EnqueueClose();

        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "enum");
        var received = new List<long>();

        await foreach (var msg in conn.ReceiveAllAsync())
            received.Add(msg.Timestamp);

        Assert.Equal(3, received.Count);
        Assert.Equal(100, received[0]);
        Assert.Equal(200, received[1]);
        Assert.Equal(300, received[2]);
    }

    [Fact]
    public async Task ReceiveAllAsync_EmptyStream_YieldsNothing()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueClose();

        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "empty");
        var received = new List<Heartbeat>();

        await foreach (var msg in conn.ReceiveAllAsync())
            received.Add(msg);

        Assert.Empty(received);
    }

    [Fact]
    public async Task ReceiveAllAsync_CancellationToken_StopsEnumeration()
    {
        var ws = new FakeWebSocket();
        ws.SetReceiveDelay(5000); // Slow receive to trigger cancellation

        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "cancel");
        using var cts = new CancellationTokenSource(50);
        var received = new List<Heartbeat>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var msg in conn.ReceiveAllAsync(cts.Token))
                received.Add(msg);
        });
    }

    #endregion

    #region Bit-Error-Simulation Pattern — malformed data handling

    [Fact]
    public async Task ReceiveAsync_MalformedProtobuf_ThrowsOnDecode()
    {
        var ws = new FakeWebSocket();

        // Write a valid varint length prefix (3 bytes) followed by garbage data
        using var ms = new MemoryStream();
        ms.WriteByte(3); // varint: length = 3
        ms.Write(new byte[] { 0xFF, 0xFF, 0xFF }); // invalid protobuf
        ws.EnqueueReceive(ms.ToArray());

        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "bad");

        // Decode should fail — either InvalidOperationException or similar
        await Assert.ThrowsAnyAsync<Exception>(() => conn.ReceiveAsync());
    }

    [Fact]
    public async Task ReceiveAsync_TruncatedMessage_Throws()
    {
        var ws = new FakeWebSocket();

        // Write a length prefix claiming 100 bytes, but provide only 2
        using var ms = new MemoryStream();
        ms.WriteByte(100); // varint: length = 100
        ms.Write(new byte[] { 0x01, 0x02 }); // only 2 bytes
        ws.EnqueueReceive(ms.ToArray());
        ws.EnqueueClose(); // stream ends before 100 bytes read

        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "trunc");

        await Assert.ThrowsAnyAsync<Exception>(() => conn.ReceiveAsync());
    }

    #endregion

    #region Constraint-Data Pattern — empty messages

    [Fact]
    public async Task SendAsync_EmptyMessage_Succeeds()
    {
        var ws = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<EmptyMessage, EmptyMessage>(ws, "c");

        var ex = await Record.ExceptionAsync(
            () => conn.SendAsync(new EmptyMessage()));
        Assert.Null(ex);
        Assert.True(ws.SendCount >= 1);
    }

    [Fact]
    public async Task ReceiveAsync_EmptyMessage_Decodes()
    {
        var ws = new FakeWebSocket();

        using var ms = new MemoryStream();
        ProtobufEncoder.WriteDelimitedMessage(new EmptyMessage(), ms);
        ws.EnqueueReceive(ms.ToArray());
        ws.EnqueueClose();

        var conn = new ProtobufWebSocketConnection<EmptyMessage, EmptyMessage>(ws, "c");
        var received = await conn.ReceiveAsync();

        Assert.NotNull(received);
    }

    #endregion

    #region Performance-Test Pattern — high throughput send

    [Fact]
    public async Task SendAsync_HighVolume_CompletesEfficiently()
    {
        var ws = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "perf");
        const int count = 5_000;

        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < count; i++)
            await conn.SendAsync(new Heartbeat { Timestamp = i });

        sw.Stop();

        // WriteDelimitedMessage writes length prefix + payload as separate frames
        Assert.True(ws.SendCount >= count,
            $"Expected at least {count} sends, got {ws.SendCount}");
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Sending {count} messages took {sw.ElapsedMilliseconds}ms");
    }

    #endregion
}
