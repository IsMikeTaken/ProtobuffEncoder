namespace ProtobuffEncoder.Tests;

/// <summary>
/// Tests for collection serialization: arrays, lists, packed scalars,
/// repeated nested messages, and empty collections.
/// </summary>
public class CollectionTests
{
    [Fact]
    public void Encode_Decode_IntList_RoundTrips()
    {
        // Arrange
        var original = new ListMessage { Numbers = [1, 2, 3, 100, -5] };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ListMessage>(bytes);

        // Assert
        Assert.Equal([1, 2, 3, 100, -5], decoded.Numbers);
    }

    [Fact]
    public void Encode_Decode_EmptyIntList_RoundTrips()
    {
        // Arrange
        var original = new ListMessage { Numbers = [] };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ListMessage>(bytes);

        // Assert
        Assert.Empty(decoded.Numbers);
    }

    [Fact]
    public void Encode_Decode_SingleElementList_RoundTrips()
    {
        // Arrange
        var original = new ListMessage { Numbers = [42] };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ListMessage>(bytes);

        // Assert
        Assert.Single(decoded.Numbers);
        Assert.Equal(42, decoded.Numbers[0]);
    }

    [Fact]
    public void Encode_Decode_LargeList_RoundTrips()
    {
        // Arrange
        var original = new ListMessage { Numbers = Enumerable.Range(0, 10_000).ToList() };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ListMessage>(bytes);

        // Assert
        Assert.Equal(10_000, decoded.Numbers.Count);
        Assert.Equal(0, decoded.Numbers[0]);
        Assert.Equal(9_999, decoded.Numbers[^1]);
    }

    [Fact]
    public void Encode_Decode_StringList_RoundTrips()
    {
        // Arrange
        var original = new ListMessage { Tags = ["alpha", "beta", "gamma"] };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ListMessage>(bytes);

        // Assert
        Assert.Equal(["alpha", "beta", "gamma"], decoded.Tags);
    }

    [Fact]
    public void Encode_Decode_EmptyStringList_RoundTrips()
    {
        // Arrange
        var original = new ListMessage { Tags = [] };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ListMessage>(bytes);

        // Assert
        Assert.Empty(decoded.Tags);
    }

    [Fact]
    public void Encode_Decode_NestedMessageList_RoundTrips()
    {
        // Arrange
        var original = new ListMessage
        {
            Items =
            [
                new NestedInner { Value = 1, Detail = "first" },
                new NestedInner { Value = 2, Detail = "second" },
                new NestedInner { Value = 3, Detail = "third" }
            ]
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ListMessage>(bytes);

        // Assert
        Assert.Equal(3, decoded.Items.Count);
        Assert.Equal("first", decoded.Items[0].Detail);
        Assert.Equal(2, decoded.Items[1].Value);
        Assert.Equal("third", decoded.Items[2].Detail);
    }

    [Fact]
    public void Encode_Decode_SingleNestedItem_RoundTrips()
    {
        // Arrange
        var original = new ListMessage
        {
            Items = [new NestedInner { Value = 99, Detail = "only" }]
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ListMessage>(bytes);

        // Assert
        Assert.Single(decoded.Items);
        Assert.Equal(99, decoded.Items[0].Value);
    }

    [Fact]
    public void Encode_Decode_IntArray_RoundTrips()
    {
        // Arrange
        var original = new ArrayMessage { Scores = [10, 20, 30] };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ArrayMessage>(bytes);

        // Assert
        Assert.Equal([10, 20, 30], decoded.Scores);
    }

    [Fact]
    public void Encode_Decode_EmptyArray_RoundTrips()
    {
        // Arrange
        var original = new ArrayMessage { Scores = [], Names = [] };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ArrayMessage>(bytes);

        // Assert
        Assert.Empty(decoded.Scores);
        Assert.Empty(decoded.Names);
    }

    [Fact]
    public void Encode_Decode_StringArray_RoundTrips()
    {
        // Arrange
        var original = new ArrayMessage { Names = ["a", "b", "c"] };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ArrayMessage>(bytes);

        // Assert
        Assert.Equal(["a", "b", "c"], decoded.Names);
    }

    [Fact]
    public void Encode_Decode_MixedCollections_RoundTrips()
    {
        // Arrange
        var original = new ListMessage
        {
            Numbers = [1, 2, 3],
            Tags = ["x", "y"],
            Items = [new NestedInner { Value = 42, Detail = "nested" }]
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ListMessage>(bytes);

        // Assert
        Assert.Equal([1, 2, 3], decoded.Numbers);
        Assert.Equal(["x", "y"], decoded.Tags);
        Assert.Single(decoded.Items);
        Assert.Equal(42, decoded.Items[0].Value);
    }

    [Fact]
    public async Task Encode_Decode_CollectionsConcurrently_AllSucceed()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
        {
            var msg = new ListMessage
            {
                Numbers = Enumerable.Range(i, 10).ToList(),
                Tags = [$"tag-{i}"]
            };

            // Act
            var bytes = ProtobufEncoder.Encode(msg);
            var decoded = ProtobufEncoder.Decode<ListMessage>(bytes);

            // Assert
            Assert.Equal(10, decoded.Numbers.Count);
            Assert.Equal($"tag-{i}", decoded.Tags[0]);
        }));

        // Act & Assert
        await Task.WhenAll(tasks);
    }
}
