using System.Net.WebSockets;
using ProtobuffEncoder.WebSockets.Tests.Fixtures;

namespace ProtobuffEncoder.WebSockets.Tests;

/// <summary>
/// Tests for the v2 direct-frame send/receive API on
/// <see cref="ProtobufWebSocketConnection{TSend,TReceive}"/>:
/// <see cref="ProtobufWebSocketConnection{TSend,TReceive}.SendDirectAsync"/>,
/// <see cref="ProtobufWebSocketConnection{TSend,TReceive}.ReceiveDirectAsync"/>, and
/// <see cref="ProtobufWebSocketConnection{TSend,TReceive}.ReceiveAllDirectAsync"/>.
/// </summary>
public class ProtobufWebSocketConnectionV2Tests
{
    // ── SendDirectAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SendDirectAsync_EncodesMessageAsRawFrame()
    {
        var ws   = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "v2-send");

        var msg = new Heartbeat { Timestamp = 999L };
        await conn.SendDirectAsync(msg);

        // Exactly one frame was sent.
        Assert.Equal(1, ws.SendCount);

        // The frame decodes back to the original message without a length prefix.
        byte[] frame = ws.SentMessages.Single();
        Heartbeat decoded = ProtobufEncoder.Decode<Heartbeat>(frame);
        Assert.Equal(999L, decoded.Timestamp);
    }

    [Fact]
    public async Task SendDirectAsync_MultipleMessages_EachIsIndependentFrame()
    {
        var ws   = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<ChatMessage, ChatMessage>(ws, "v2-multi");

        await conn.SendDirectAsync(new ChatMessage { User = "Alice", Text = "hi" });
        await conn.SendDirectAsync(new ChatMessage { User = "Bob",   Text = "hello" });

        Assert.Equal(2, ws.SendCount);

        byte[][] frames = ws.SentMessages.ToArray();
        Assert.Equal("Alice", ProtobufEncoder.Decode<ChatMessage>(frames[0]).User);
        Assert.Equal("Bob",   ProtobufEncoder.Decode<ChatMessage>(frames[1]).User);
    }

    [Fact]
    public async Task SendDirectAsync_LargePayload_RoundTrips()
    {
        var ws   = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<LargePayload, LargePayload>(ws, "v2-large");

        string bigData = new string('x', 128 * 1024); // 128 KiB string
        await conn.SendDirectAsync(new LargePayload { Data = bigData, SequenceNumber = 42 });

        byte[] frame   = ws.SentMessages.Single();
        LargePayload rt = ProtobufEncoder.Decode<LargePayload>(frame);

        Assert.Equal(bigData, rt.Data);
        Assert.Equal(42, rt.SequenceNumber);
    }

    [Fact]
    public async Task SendDirectAsync_CancelledToken_Throws()
    {
        var ws   = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "v2-cancel");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => conn.SendDirectAsync(new Heartbeat(), cts.Token).AsTask());
    }

    // ── ReceiveDirectAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ReceiveDirectAsync_DecodesRawFrame()
    {
        var ws  = new FakeWebSocket();
        var msg = new Heartbeat { Timestamp = 12345L };
        // EnqueueMessage encodes with ProtobufEncoder.Encode (no length prefix) — matches v2 framing.
        ws.EnqueueMessage(msg);

        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "v2-recv");

        Heartbeat? received = await conn.ReceiveDirectAsync();

        Assert.NotNull(received);
        Assert.Equal(12345L, received.Timestamp);
    }

    [Fact]
    public async Task ReceiveDirectAsync_GracefulClose_ReturnsNull()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueClose();

        var conn     = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "v2-close");
        Heartbeat? r = await conn.ReceiveDirectAsync();

        Assert.Null(r);
    }

    [Fact]
    public async Task ReceiveDirectAsync_EmptyQueue_ReturnsNull()
    {
        // FakeWebSocket with empty queue returns Close automatically.
        var ws   = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "v2-empty");

        Heartbeat? r = await conn.ReceiveDirectAsync();

        Assert.Null(r);
    }

    [Fact]
    public async Task ReceiveDirectAsync_MultipleMessages_DecodesInOrder()
    {
        var ws = new FakeWebSocket();
        ws.EnqueueMessage(new ChatMessage { User = "Alice", Text = "first"  });
        ws.EnqueueMessage(new ChatMessage { User = "Bob",   Text = "second" });
        ws.EnqueueClose();

        var conn = new ProtobufWebSocketConnection<ChatMessage, ChatMessage>(ws, "v2-order");

        ChatMessage? m1 = await conn.ReceiveDirectAsync();
        ChatMessage? m2 = await conn.ReceiveDirectAsync();
        ChatMessage? m3 = await conn.ReceiveDirectAsync();

        Assert.Equal("Alice", m1!.User);
        Assert.Equal("Bob",   m2!.User);
        Assert.Null(m3);
    }

    // ── ReceiveAllDirectAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ReceiveAllDirectAsync_YieldsAllMessagesUntilClose()
    {
        var ws = new FakeWebSocket();
        for (int i = 0; i < 5; i++)
            ws.EnqueueMessage(new Heartbeat { Timestamp = i });
        ws.EnqueueClose();

        var conn     = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "v2-all");
        var received = new List<long>();

        await foreach (Heartbeat msg in conn.ReceiveAllDirectAsync())
            received.Add(msg.Timestamp);

        Assert.Equal([0L, 1L, 2L, 3L, 4L], received);
    }

    [Fact]
    public async Task ReceiveAllDirectAsync_CancellationStopsEnumeration()
    {
        var ws = new FakeWebSocket();
        // Queue more messages than we'll ever read.
        for (int i = 0; i < 100; i++)
            ws.EnqueueMessage(new Heartbeat { Timestamp = i });

        using var cts = new CancellationTokenSource();
        var conn      = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "v2-cancel-all");
        int count     = 0;

        // Cancellation causes the loop guard to exit cleanly — no exception propagated.
        await foreach (Heartbeat _ in conn.ReceiveAllDirectAsync(cts.Token))
        {
            count++;
            if (count == 3) cts.Cancel();
        }

        // Should have stopped well before 100 (at most a few messages past the cancel).
        Assert.True(count <= 10,
            $"Expected to stop early after cancellation, but received {count} messages");
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendDirectAsync_ReceiveDirectAsync_RoundTrip()
    {
        // Simulate a loopback: capture the sent frame and re-enqueue it for receive.
        var sendWs = new FakeWebSocket();
        var conn   = new ProtobufWebSocketConnection<ChatMessage, ChatMessage>(sendWs, "v2-rt");

        var original = new ChatMessage { User = "Tester", Text = "round-trip", SentAt = 42L };
        await conn.SendDirectAsync(original);

        // Wire the sent frame back as a receive.
        byte[] frame = sendWs.SentMessages.Single();
        var recvWs   = new FakeWebSocket();
        recvWs.EnqueueReceive(frame);
        var recvConn = new ProtobufWebSocketConnection<ChatMessage, ChatMessage>(recvWs, "v2-rt-recv");

        ChatMessage? result = await recvConn.ReceiveDirectAsync();

        Assert.NotNull(result);
        Assert.Equal("Tester",     result.User);
        Assert.Equal("round-trip", result.Text);
        Assert.Equal(42L,          result.SentAt);
    }

    // ── Performance ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SendDirectAsync_HighVolume_CompletesWithinBudget()
    {
        var ws   = new FakeWebSocket();
        var conn = new ProtobufWebSocketConnection<Heartbeat, Heartbeat>(ws, "v2-perf");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
            await conn.SendDirectAsync(new Heartbeat { Timestamp = i });
        sw.Stop();

        Assert.Equal(10_000, ws.SendCount);
        Assert.True(sw.ElapsedMilliseconds < 3_000,
            $"10k direct sends took {sw.ElapsedMilliseconds} ms — expected < 3000 ms");
    }
}
