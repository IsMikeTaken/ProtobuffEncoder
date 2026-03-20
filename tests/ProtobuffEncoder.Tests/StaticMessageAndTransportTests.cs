namespace ProtobuffEncoder.Tests;

/// <summary>
/// Tests for StaticMessage, pre-compiled encoder/decoder delegates,
/// and the ProtobufSender/ProtobufReceiver transport classes.
/// </summary>
public class StaticMessageAndTransportTests
{
    [Fact]
    public void StaticMessage_Encode_Decode_RoundTrips()
    {
        // Arrange
        var staticMsg = ProtobufEncoder.CreateStaticMessage<SimpleMessage>();
        var original = new SimpleMessage { Id = 42, Name = "static" };

        // Act
        var bytes = staticMsg.Encode(original);
        var decoded = staticMsg.Decode(bytes);

        // Assert
        Assert.Equal(42, decoded.Id);
        Assert.Equal("static", decoded.Name);
    }

    [Fact]
    public void StaticEncoder_ProducesIdenticalBytes()
    {
        // Arrange
        var encoder = ProtobufEncoder.CreateStaticEncoder<SimpleMessage>();
        var msg = new SimpleMessage { Id = 7, Name = "compare" };

        // Act
        var staticBytes = encoder(msg);
        var directBytes = ProtobufEncoder.Encode(msg);

        // Assert
        Assert.Equal(directBytes, staticBytes);
    }

    [Fact]
    public void StaticDecoder_DecodesCorrectly()
    {
        // Arrange
        var decoder = ProtobufEncoder.CreateStaticDecoder<SimpleMessage>();
        var original = new SimpleMessage { Id = 99, Name = "decode-test" };
        var bytes = ProtobufEncoder.Encode(original);

        // Act
        var decoded = decoder(bytes);

        // Assert
        Assert.Equal(99, decoded.Id);
        Assert.Equal("decode-test", decoded.Name);
    }

    [Fact]
    public void StaticMessage_WriteDelimited_ReadDelimited_RoundTrips()
    {
        // Arrange
        var staticMsg = ProtobufEncoder.CreateStaticMessage<SimpleMessage>();
        using var stream = new MemoryStream();
        var original = new SimpleMessage { Id = 5, Name = "delimited-static" };

        // Act
        staticMsg.WriteDelimited(original, stream);
        stream.Position = 0;
        var decoded = staticMsg.ReadDelimited(stream);

        // Assert
        Assert.NotNull(decoded);
        Assert.Equal(5, decoded.Id);
        Assert.Equal("delimited-static", decoded.Name);
    }

    [Fact]
    public async Task StaticMessage_WriteDelimitedAsync_Works()
    {
        // Arrange
        var staticMsg = ProtobufEncoder.CreateStaticMessage<SimpleMessage>();
        using var stream = new MemoryStream();
        var original = new SimpleMessage { Id = 3, Name = "async-static" };

        // Act
        await staticMsg.WriteDelimitedAsync(original, stream);
        stream.Position = 0;
        var decoded = staticMsg.ReadDelimited(stream);

        // Assert
        Assert.NotNull(decoded);
        Assert.Equal(3, decoded.Id);
    }

    [Fact]
    public void StaticMessage_ReadDelimited_EmptyStream_ReturnsNull()
    {
        // Arrange
        var staticMsg = ProtobufEncoder.CreateStaticMessage<SimpleMessage>();
        using var stream = new MemoryStream();

        // Act
        var result = staticMsg.ReadDelimited(stream);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task StaticEncoder_ConcurrentUsage_AllSucceed()
    {
        // Arrange
        var encoder = ProtobufEncoder.CreateStaticEncoder<SimpleMessage>();
        var decoder = ProtobufEncoder.CreateStaticDecoder<SimpleMessage>();

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
        {
            var msg = new SimpleMessage { Id = i, Name = $"concurrent-{i}" };
            var bytes = encoder(msg);
            var decoded = decoder(bytes);
            Assert.Equal(i, decoded.Id);
        }));

        // Act & Assert
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void Sender_Send_WritesToStream()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var sender = new ProtobuffEncoder.Transport.ProtobufSender<SimpleMessage>(stream, ownsStream: false);

        // Act
        sender.Send(new SimpleMessage { Id = 1, Name = "sent" });

        // Assert
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public async Task Sender_SendAsync_WritesToStream()
    {
        // Arrange
        using var stream = new MemoryStream();
        await using var sender = new ProtobuffEncoder.Transport.ProtobufSender<SimpleMessage>(stream, ownsStream: false);

        // Act
        await sender.SendAsync(new SimpleMessage { Id = 2 });

        // Assert
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public async Task Sender_SendManyAsync_Enumerable_WritesAll()
    {
        // Arrange
        using var stream = new MemoryStream();
        await using var sender = new ProtobuffEncoder.Transport.ProtobufSender<SimpleMessage>(stream, ownsStream: false);
        var messages = Enumerable.Range(0, 5)
            .Select(i => new SimpleMessage { Id = i });

        // Act
        await sender.SendManyAsync(messages);
        stream.Position = 0;
        var decoded = ProtobufEncoder.ReadDelimitedMessages<SimpleMessage>(stream).ToList();

        // Assert
        Assert.Equal(5, decoded.Count);
    }

    [Fact]
    public void Receiver_Receive_ReadsFromStream()
    {
        // Arrange
        using var stream = new MemoryStream();
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 10, Name = "received" }, stream);
        stream.Position = 0;
        using var receiver = new ProtobuffEncoder.Transport.ProtobufReceiver<SimpleMessage>(stream, ownsStream: false);

        // Act
        var msg = receiver.Receive();

        // Assert
        Assert.NotNull(msg);
        Assert.Equal(10, msg.Id);
        Assert.Equal("received", msg.Name);
    }

    [Fact]
    public void Receiver_Receive_EmptyStream_ReturnsNull()
    {
        // Arrange
        using var stream = new MemoryStream();
        using var receiver = new ProtobuffEncoder.Transport.ProtobufReceiver<SimpleMessage>(stream, ownsStream: false);

        // Act
        var msg = receiver.Receive();

        // Assert
        Assert.Null(msg);
    }

    [Fact]
    public void Receiver_ReceiveAll_ReadsMultiple()
    {
        // Arrange
        using var stream = new MemoryStream();
        for (int i = 0; i < 3; i++)
            ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = i }, stream);
        stream.Position = 0;
        using var receiver = new ProtobuffEncoder.Transport.ProtobufReceiver<SimpleMessage>(stream, ownsStream: false);

        // Act
        var messages = receiver.ReceiveAll().ToList();

        // Assert
        Assert.Equal(3, messages.Count);
        Assert.Equal(0, messages[0].Id);
        Assert.Equal(2, messages[2].Id);
    }

    [Fact]
    public async Task Receiver_ReceiveAllAsync_ReadsMultiple()
    {
        // Arrange
        using var stream = new MemoryStream();
        for (int i = 0; i < 4; i++)
            ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = i }, stream);
        stream.Position = 0;
        await using var receiver = new ProtobuffEncoder.Transport.ProtobufReceiver<SimpleMessage>(stream, ownsStream: false);

        // Act
        var messages = new List<SimpleMessage>();
        await foreach (var msg in receiver.ReceiveAllAsync())
            messages.Add(msg);

        // Assert
        Assert.Equal(4, messages.Count);
    }

    [Fact]
    public async Task Receiver_ListenAsync_InvokesHandler()
    {
        // Arrange
        using var stream = new MemoryStream();
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 1 }, stream);
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 2 }, stream);
        stream.Position = 0;
        await using var receiver = new ProtobuffEncoder.Transport.ProtobufReceiver<SimpleMessage>(stream, ownsStream: false);

        var received = new List<int>();

        // Act
        await receiver.ListenAsync(msg =>
        {
            received.Add(msg.Id);
            return Task.CompletedTask;
        });

        // Assert
        Assert.Equal([1, 2], received);
    }

    [Fact]
    public void DuplexStream_SendAndReceive_SingleStream_RoundTrips()
    {
        // Arrange — use separate streams for send/receive since single MemoryStream can't do bi-di
        using var sendStream = new MemoryStream();
        using var receiveStream = new MemoryStream();

        using var duplex = new ProtobuffEncoder.Transport.ProtobufDuplexStream<SimpleMessage, SimpleMessage>(
            sendStream, receiveStream, ownsStreams: false);

        // Write to receive stream first
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 99 }, receiveStream);
        receiveStream.Position = 0;

        // Act — send and receive
        duplex.Send(new SimpleMessage { Id = 1, Name = "sent" });
        var received = duplex.Receive();

        // Assert
        Assert.True(sendStream.Length > 0);
        Assert.NotNull(received);
        Assert.Equal(99, received.Id);
    }

    [Fact]
    public async Task DuplexStream_SendAndReceiveAsync_RoundTrips()
    {
        // Arrange
        using var sendStream = new MemoryStream();
        using var receiveStream = new MemoryStream();
        await using var duplex = new ProtobuffEncoder.Transport.ProtobufDuplexStream<SimpleMessage, SimpleMessage>(
            sendStream, receiveStream, ownsStreams: false);

        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 77 }, receiveStream);
        receiveStream.Position = 0;

        // Act
        await duplex.SendAsync(new SimpleMessage { Id = 1 });
        var result = await duplex.ReceiveAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(77, result.Id);
    }

    [Fact]
    public async Task DuplexStream_SendAndReceiveAsync_Method_RoundTrips()
    {
        // Arrange
        using var sendStream = new MemoryStream();
        using var receiveStream = new MemoryStream();
        await using var duplex = new ProtobuffEncoder.Transport.ProtobufDuplexStream<SimpleMessage, SimpleMessage>(
            sendStream, receiveStream, ownsStreams: false);

        // Pre-load response
        ProtobufEncoder.WriteDelimitedMessage(new SimpleMessage { Id = 42 }, receiveStream);
        receiveStream.Position = 0;

        // Act — request/response pattern
        var response = await duplex.SendAndReceiveAsync(new SimpleMessage { Id = 1 });

        // Assert
        Assert.NotNull(response);
        Assert.Equal(42, response.Id);
    }
}
