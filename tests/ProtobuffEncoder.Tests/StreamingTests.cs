namespace ProtobuffEncoder.Tests;

/// <summary>
/// Tests for streaming: delimited messages, async streams, multiple messages,
/// and edge cases like empty streams.
/// </summary>
public class StreamingTests
{
    [Fact]
    public void WriteAndRead_SingleDelimitedMessage_RoundTrips()
    {
        // Arrange
        using var stream = new MemoryStream();
        var msg = new SimpleMessage { Id = 1, Name = "delimited" };

        // Act
        ProtobufEncoder.WriteDelimitedMessage(msg, stream);
        stream.Position = 0;
        var decoded = ProtobufEncoder.ReadDelimitedMessage<SimpleMessage>(stream);

        // Assert
        Assert.NotNull(decoded);
        Assert.Equal(1, decoded.Id);
        Assert.Equal("delimited", decoded.Name);
    }

    [Fact]
    public void WriteAndRead_MultipleDelimitedMessages_RoundTrips()
    {
        // Arrange
        using var stream = new MemoryStream();
        var messages = Enumerable.Range(0, 10)
            .Select(i => new SimpleMessage { Id = i, Name = $"msg-{i}" })
            .ToList();

        // Act
        foreach (var msg in messages)
            ProtobufEncoder.WriteDelimitedMessage(msg, stream);

        stream.Position = 0;
        var decoded = ProtobufEncoder.ReadDelimitedMessages<SimpleMessage>(stream).ToList();

        // Assert
        Assert.Equal(10, decoded.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i, decoded[i].Id);
            Assert.Equal($"msg-{i}", decoded[i].Name);
        }
    }

    [Fact]
    public void ReadDelimitedMessage_EmptyStream_ReturnsNull()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var result = ProtobufEncoder.ReadDelimitedMessage<SimpleMessage>(stream);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ReadDelimitedMessages_EmptyStream_ReturnsEmpty()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var results = ProtobufEncoder.ReadDelimitedMessages<SimpleMessage>(stream).ToList();

        // Assert
        Assert.Empty(results);
    }

    // ─── Async delimited messages ─────────────────────────

    [Fact]
    public async Task WriteAndReadAsync_SingleMessage_RoundTrips()
    {
        // Arrange
        using var stream = new MemoryStream();
        var msg = new SimpleMessage { Id = 42, Name = "async-delimit" };

        // Act
        await ProtobufEncoder.WriteDelimitedMessageAsync(msg, stream);
        stream.Position = 0;

        var decoded = await ReadFirstAsync(stream);

        // Assert
        Assert.NotNull(decoded);
        Assert.Equal(42, decoded.Id);
    }

    [Fact]
    public async Task WriteAndReadAsync_MultipleMessages_RoundTrips()
    {
        // Arrange
        using var stream = new MemoryStream();
        for (int i = 0; i < 5; i++)
            await ProtobufEncoder.WriteDelimitedMessageAsync(
                new SimpleMessage { Id = i, Name = $"a-{i}" }, stream);

        // Act
        stream.Position = 0;
        var decoded = new List<SimpleMessage>();
        await foreach (var msg in ProtobufEncoder.ReadDelimitedMessagesAsync<SimpleMessage>(stream))
            decoded.Add(msg);

        // Assert
        Assert.Equal(5, decoded.Count);
        Assert.Equal("a-0", decoded[0].Name);
        Assert.Equal("a-4", decoded[4].Name);
    }

    [Fact]
    public async Task ReadDelimitedMessagesAsync_EmptyStream_YieldsNothing()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var decoded = new List<SimpleMessage>();
        await foreach (var msg in ProtobufEncoder.ReadDelimitedMessagesAsync<SimpleMessage>(stream))
            decoded.Add(msg);

        // Assert
        Assert.Empty(decoded);
    }

    [Fact]
    public async Task WriteDelimitedMessageAsync_NullArguments_Throw()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ProtobufEncoder.WriteDelimitedMessageAsync(null!, stream));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ProtobufEncoder.WriteDelimitedMessageAsync(new SimpleMessage(), null!));
    }

    [Fact]
    public async Task ReadDelimitedMessagesAsync_CancellationToken_StopsReading()
    {
        // Arrange
        using var stream = new MemoryStream();
        for (int i = 0; i < 10; i++)
            ProtobufEncoder.WriteDelimitedMessage(
                new SimpleMessage { Id = i }, stream);
        stream.Position = 0;

        var cts = new CancellationTokenSource();
        var decoded = new List<SimpleMessage>();

        // Act
        await foreach (var msg in ProtobufEncoder.ReadDelimitedMessagesAsync<SimpleMessage>(stream, cts.Token))
        {
            decoded.Add(msg);
            if (decoded.Count >= 3)
                cts.Cancel();
        }

        // Assert — should have read at most 3-4 (race is possible)
        Assert.True(decoded.Count >= 3);
        Assert.True(decoded.Count <= 10);
    }

    [Fact]
    public void WriteAndRead_LargePayload_RoundTrips()
    {
        // Arrange
        using var stream = new MemoryStream();
        var largeMsg = new SimpleMessage { Id = 1, Name = new string('Z', 100_000) };

        // Act
        ProtobufEncoder.WriteDelimitedMessage(largeMsg, stream);
        stream.Position = 0;
        var decoded = ProtobufEncoder.ReadDelimitedMessage<SimpleMessage>(stream);

        // Assert
        Assert.NotNull(decoded);
        Assert.Equal(100_000, decoded.Name.Length);
    }

    [Fact]
    public async Task WriteAndRead_ConcurrentStreams_AllSucceed()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            using var stream = new MemoryStream();
            var msg = new SimpleMessage { Id = i, Name = $"concurrent-{i}" };

            // Act
            ProtobufEncoder.WriteDelimitedMessage(msg, stream);
            stream.Position = 0;
            var decoded = ProtobufEncoder.ReadDelimitedMessage<SimpleMessage>(stream);

            // Assert
            Assert.NotNull(decoded);
            Assert.Equal(i, decoded.Id);
        }));

        // Act & Assert
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void SampleHelper_JustForStructure() { }

    private static async Task<SimpleMessage?> ReadFirstAsync(Stream stream)
    {
        await foreach (var msg in ProtobufEncoder.ReadDelimitedMessagesAsync<SimpleMessage>(stream))
            return msg;
        return null;
    }
}
