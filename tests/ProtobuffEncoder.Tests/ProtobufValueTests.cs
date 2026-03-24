using ProtobuffEncoder.Transport;
using System.Text;

namespace ProtobuffEncoder.Tests;

/// <summary>
/// Tests for ProtobufValueSender and ProtobufValueReceiver.
/// </summary>
public class ProtobufValueTests
{
    [Fact]
    public void RoundTrip_String_Works()
    {
        using var stream = new MemoryStream();
        using var sender = new ProtobufValueSender(stream, ownsStream: false);
        sender.Send("Hello World 🌍");
        
        stream.Position = 0;
        using var receiver = new ProtobufValueReceiver(stream, ownsStream: false);
        var result = receiver.ReceiveString();
        
        Assert.Equal("Hello World 🌍", result);
    }

    [Fact]
    public void RoundTrip_Int32_Works()
    {
        using var stream = new MemoryStream();
        using var sender = new ProtobufValueSender(stream, ownsStream: false);
        sender.Send(12345);
        
        stream.Position = 0;
        using var receiver = new ProtobufValueReceiver(stream, ownsStream: false);
        var result = receiver.ReceiveInt32();
        
        Assert.Equal(12345, result);
    }

    [Fact]
    public void RoundTrip_Guid_Works()
    {
        var guid = Guid.NewGuid();
        using var stream = new MemoryStream();
        using var sender = new ProtobufValueSender(stream, ownsStream: false);
        sender.Send(guid);
        
        stream.Position = 0;
        using var receiver = new ProtobufValueReceiver(stream, ownsStream: false);
        var result = receiver.ReceiveGuid();
        
        Assert.Equal(guid, result);
    }

    [Fact]
    public async Task RoundTrip_AsyncManyStrings_Works()
    {
        var inputs = new[] { "One", "Two", "Three" };
        using var stream = new MemoryStream();
        await using var sender = new ProtobufValueSender(stream, ownsStream: false);
        await sender.SendManyAsync(inputs.ToAsyncEnumerable());
        
        stream.Position = 0;
        await using var receiver = new ProtobufValueReceiver(stream, ownsStream: false);
        var results = new List<string>();

        await foreach (var s in receiver.ReceiveAllStringsAsync())
            results.Add(s);
        
        Assert.Equal(inputs, results);
    }

    [Fact]
    public void Receive_EndOfStream_ReturnsNull()
    {
        using var stream = new MemoryStream();
        using var receiver = new ProtobufValueReceiver(stream, ownsStream: false);
        
        Assert.Null(receiver.ReceiveString());
        Assert.Null(receiver.ReceiveInt32());
    }

    [Fact]
    public void Receive_TruncatedVarint_Throws()
    {
        using var stream = new MemoryStream(new byte[] { 0x80 }); // MSB set but no next byte
        using var receiver = new ProtobufValueReceiver(stream, ownsStream: false);
        
        Assert.Throws<InvalidOperationException>(() => receiver.ReceiveInt32());
    }
}

internal static class EnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
