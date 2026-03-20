namespace ProtobuffEncoder.Tests;

/// <summary>
/// Tests for dictionary/map field serialization.
/// </summary>
public class MapFieldTests
{
    [Fact]
    public void Encode_Decode_StringStringMap_RoundTrips()
    {
        // Arrange
        var original = new MapMessage
        {
            Tags = new Dictionary<string, string>
            {
                ["env"] = "production",
                ["region"] = "eu-west-1",
                ["tier"] = "premium"
            }
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<MapMessage>(bytes);

        // Assert
        Assert.Equal(3, decoded.Tags.Count);
        Assert.Equal("production", decoded.Tags["env"]);
        Assert.Equal("eu-west-1", decoded.Tags["region"]);
        Assert.Equal("premium", decoded.Tags["tier"]);
    }

    [Fact]
    public void Encode_Decode_EmptyMap_RoundTrips()
    {
        // Arrange
        var original = new MapMessage { Tags = new(), ItemMap = new() };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<MapMessage>(bytes);

        // Assert
        Assert.Empty(decoded.Tags);
        Assert.Empty(decoded.ItemMap);
    }

    [Fact]
    public void Encode_Decode_SingleEntryMap_RoundTrips()
    {
        // Arrange
        var original = new MapMessage
        {
            Tags = new() { ["single"] = "value" }
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<MapMessage>(bytes);

        // Assert
        Assert.Single(decoded.Tags);
        Assert.Equal("value", decoded.Tags["single"]);
    }

    [Fact]
    public void Encode_Decode_IntToNestedMessageMap_RoundTrips()
    {
        // Arrange
        var original = new MapMessage
        {
            ItemMap = new Dictionary<int, NestedInner>
            {
                [1] = new NestedInner { Value = 100, Detail = "first" },
                [2] = new NestedInner { Value = 200, Detail = "second" }
            }
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<MapMessage>(bytes);

        // Assert
        Assert.Equal(2, decoded.ItemMap.Count);
        Assert.Equal(100, decoded.ItemMap[1].Value);
        Assert.Equal("first", decoded.ItemMap[1].Detail);
        Assert.Equal(200, decoded.ItemMap[2].Value);
    }

    [Fact]
    public void Encode_Decode_LargeMap_RoundTrips()
    {
        // Arrange
        var original = new MapMessage
        {
            Tags = Enumerable.Range(0, 500)
                .ToDictionary(i => $"key-{i}", i => $"value-{i}")
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<MapMessage>(bytes);

        // Assert
        Assert.Equal(500, decoded.Tags.Count);
        Assert.Equal("value-0", decoded.Tags["key-0"]);
        Assert.Equal("value-499", decoded.Tags["key-499"]);
    }

    [Fact]
    public void Encode_Decode_MapWithUnicodeKeys_RoundTrips()
    {
        // Arrange
        var original = new MapMessage
        {
            Tags = new() { ["clé_française"] = "valeur", ["日本語"] = "テスト" }
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<MapMessage>(bytes);

        // Assert
        Assert.Equal("valeur", decoded.Tags["clé_française"]);
        Assert.Equal("テスト", decoded.Tags["日本語"]);
    }

    [Fact]
    public async Task Encode_Decode_MapsConcurrently_AllSucceed()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
        {
            var msg = new MapMessage
            {
                Tags = new() { [$"k-{i}"] = $"v-{i}" }
            };

            // Act
            var bytes = ProtobufEncoder.Encode(msg);
            var decoded = ProtobufEncoder.Decode<MapMessage>(bytes);

            // Assert
            Assert.Equal($"v-{i}", decoded.Tags[$"k-{i}"]);
        }));

        // Act & Assert
        await Task.WhenAll(tasks);
    }
}
