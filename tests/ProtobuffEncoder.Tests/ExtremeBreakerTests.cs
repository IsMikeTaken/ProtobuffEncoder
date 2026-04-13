using ProtobuffEncoder.Attributes;
using System.Numerics;

namespace ProtobuffEncoder.Tests;

public class ExtremeBreakerTests
{
    [ProtoContract]
    public class MegaMixedMessage
    {
        [ProtoField(1)] public Guid Guid { get; set; }
        [ProtoField(2)] public decimal Decimal { get; set; }
        [ProtoField(3)] public Int128 Int128 { get; set; }
        [ProtoField(4)] public UInt128 UInt128 { get; set; }
        [ProtoField(5)] public DateTime DateTime { get; set; }
        [ProtoField(6)] public DateTimeOffset DateTimeOffset { get; set; }
        [ProtoField(7)] public TimeSpan TimeSpan { get; set; }
        [ProtoField(8)] public double Double { get; set; }
        [ProtoField(9)] public float Float { get; set; }
        [ProtoField(10)] public Half Half { get; set; }
        [ProtoField(11)] public BigInteger BigInt { get; set; }
        [ProtoField(12)] public Complex Complex { get; set; }
        [ProtoField(13)] public string String { get; set; } = string.Empty;
        [ProtoField(14)] public byte[] Bytes { get; set; } = Array.Empty<byte>();
        [ProtoField(15)] public Version Version { get; set; } = new(1, 0);
        [ProtoField(16)] public Uri Uri { get; set; } = new("http://temp");
        [ProtoField(17)] public nint NInt { get; set; }
        [ProtoField(18)] public nuint NUint { get; set; }
        [ProtoField(19)] public List<NestedObject> Children { get; set; } = new();
        [ProtoField(20)] public Dictionary<string, NestedObject> Map { get; set; } = new();
    }

    [ProtoContract]
    public class NestedObject
    {
        [ProtoField(1)] public string Name { get; set; } = string.Empty;
        [ProtoField(2)] public NestedObject? Child { get; set; }
    }

    /*
     * Bulk-Data-Stress-Test Pattern: Validates performance and stability when 
     * encoding/decoding a complex message containing every supported type 
     * plus large collections of nested objects.
     */
    [Fact]
    public void Roundtrip_MegaMixedMessage_WithLargeCollections()
    {
        // Arrange
        var original = new MegaMixedMessage
        {
            Guid = Guid.NewGuid(),
            Decimal = 123456789.987654321m,
            Int128 = Int128.MaxValue,
            UInt128 = UInt128.MaxValue,
            DateTime = DateTime.UtcNow,
            DateTimeOffset = DateTimeOffset.Now,
            TimeSpan = TimeSpan.FromTicks(long.MaxValue / 2),
            Double = double.MaxValue,
            Float = float.MaxValue,
            Half = (Half)123.45,
            BigInt = BigInteger.Pow(2, 256),
            Complex = new Complex(12.3, 45.6),
            String = new string('A', 1000),
            Bytes = new byte[1000],
            Version = new Version(4, 5, 6, 7),
            Uri = new Uri("https://very-long-and-complex-url.com/path?query=123"),
            NInt = (nint)int.MaxValue,
            NUint = (nuint)uint.MaxValue
        };

        for (int i = 0; i < 100; i++)
        {
            original.Children.Add(new NestedObject { Name = $"Child {i}" });
            original.Map[$"Key {i}"] = new NestedObject { Name = $"Map Value {i}" };
        }

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<MegaMixedMessage>(bytes);

        // Assert
        Assert.Equal(original.Guid, decoded.Guid);
        Assert.Equal(original.Decimal, decoded.Decimal);
        Assert.Equal(original.Int128, decoded.Int128);
        Assert.Equal(original.BigInt, decoded.BigInt);
        Assert.Equal(original.String.Length, decoded.String.Length);
        Assert.Equal(100, decoded.Children.Count);
        Assert.Equal(100, decoded.Map.Count);
        Assert.Equal("Child 99", decoded.Children[99].Name);
    }

    /*
     * Code-Path Pattern: Tests deep recursion (100 levels) to ensure the 
     * recursive encoder/decoder handles deep hierarchies without StackOverflow.
     */
    [Fact]
    public void DeepNesting_Roundtrip_Succeeds()
    {
        // Arrange
        var root = new NestedObject { Name = "Root" };
        var current = root;
        for (int i = 1; i <= 100; i++)
        {
            current.Child = new NestedObject { Name = $"Level {i}" };
            current = current.Child;
        }

        // Act
        var bytes = ProtobufEncoder.Encode(root);
        var decoded = ProtobufEncoder.Decode<NestedObject>(bytes);

        // Assert
        var check = decoded;
        for (int i = 0; i <= 100; i++)
        {
            Assert.NotNull(check);
            check = check.Child;
        }
        Assert.Null(check);
    }

    /*
     * Bit-Error-Simulation Pattern: Feeds deterministically malformed protobuf
     * data to the decoder to ensure it fails instead of hanging or crashing.
     * This keeps the test repeatable.
     */
    [Fact]
    public void Decode_MalformedData_ThrowsException()
    {
        // Field 1, wire type 2 (length-delimited), claims 5 bytes,
        // but only 2 bytes follow.
        byte[] malformed = [0x0A, 0x05, 0x01, 0x02];

        Assert.ThrowsAny<Exception>(() => ProtobufEncoder.Decode<MegaMixedMessage>(malformed));
    }

    /*
     * Breaker Pattern: Pushes much larger strings, byte arrays, and collection
     * sizes through a single roundtrip to stress size handling.
     */
    [Fact]
    public void Roundtrip_MegaMixedMessage_WithHugePayloadsAndCollections()
    {
        // Arrange
        var original = CreateMegaMixedMessage(
            childCount: 1_000,
            mapCount: 1_000,
            stringLength: 32_000,
            byteLength: 64_000);

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<MegaMixedMessage>(bytes);

        // Assert
        Assert.Equal(original.Guid, decoded.Guid);
        Assert.Equal(original.Decimal, decoded.Decimal);
        Assert.Equal(original.Int128, decoded.Int128);
        Assert.Equal(original.UInt128, decoded.UInt128);
        Assert.Equal(original.BigInt, decoded.BigInt);
        Assert.Equal(original.String.Length, decoded.String.Length);
        Assert.Equal(original.Bytes.Length, decoded.Bytes.Length);
        Assert.Equal(1_000, decoded.Children.Count);
        Assert.Equal(1_000, decoded.Map.Count);
        Assert.Equal("Child 999", decoded.Children[999].Name);
        Assert.Equal("Map Value 999", decoded.Map["Key 999"].Name);
    }

    /*
     * Breaker Pattern: Repeats large encode/decode cycles to catch state bleed,
     * caching bugs, or accidental mutation across runs.
     */
    [Fact]
    public void Roundtrip_MegaMixedMessage_RepeatedLargeRoundtrips_Succeed()
    {
        // Arrange
        var original = CreateMegaMixedMessage(
            childCount: 250,
            mapCount: 250,
            stringLength: 8_000,
            byteLength: 16_000);

        // Act / Assert
        for (int i = 0; i < 25; i++)
        {
            var bytes = ProtobufEncoder.Encode(original);
            var decoded = ProtobufEncoder.Decode<MegaMixedMessage>(bytes);

            Assert.Equal(original.Guid, decoded.Guid);
            Assert.Equal(original.Decimal, decoded.Decimal);
            Assert.Equal(original.Int128, decoded.Int128);
            Assert.Equal(original.UInt128, decoded.UInt128);
            Assert.Equal(original.String, decoded.String);
            Assert.Equal(original.Bytes.Length, decoded.Bytes.Length);
            Assert.Equal(original.Children.Count, decoded.Children.Count);
            Assert.Equal(original.Map.Count, decoded.Map.Count);
            Assert.Equal("Child 249", decoded.Children[249].Name);
            Assert.Equal("Map Value 249", decoded.Map["Key 249"].Name);
        }
    }

    /*
     * Breaker Pattern: Pushes recursion significantly deeper than the normal
     * roundtrip test to stress nested-message handling.
     */
    [Fact]
    public void DeepNesting_Roundtrip_At500Levels_Succeeds()
    {
        // Arrange
        var root = new NestedObject { Name = "Root" };
        var current = root;

        for (int i = 1; i <= 500; i++)
        {
            current.Child = new NestedObject { Name = $"Level {i}" };
            current = current.Child;
        }

        // Act
        var bytes = ProtobufEncoder.Encode(root);
        var decoded = ProtobufEncoder.Decode<NestedObject>(bytes);

        // Assert
        var check = decoded;
        for (int i = 0; i <= 500; i++)
        {
            Assert.NotNull(check);
            check = check.Child;
        }

        Assert.Null(check);
    }

    /*
     * Breaker Pattern: Corrupts a valid payload by truncating it at multiple
     * points. This is more realistic than random junk and remains deterministic.
     */
    [Fact]
    public void Decode_TruncatedValidPayload_ThrowsException()
    {
        // Arrange
        var original = CreateMegaMixedMessage(
            childCount: 25,
            mapCount: 25,
            stringLength: 2_000,
            byteLength: 4_000);

        var bytes = ProtobufEncoder.Encode(original);

        // Remove the tail to simulate transport/file truncation.
        var truncated = bytes[..(bytes.Length - 10)];

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => ProtobufEncoder.Decode<MegaMixedMessage>(truncated));
    }

    /*
     * Breaker Pattern: Runs a small corpus of known-bad protobuf payloads.
     * This gives broader malformed-data coverage without randomness.
     */
    [Theory]
    [MemberData(nameof(GetMalformedPayloads))]
    public void Decode_MalformedCorpus_ThrowsException(byte[] malformed)
    {
        Assert.ThrowsAny<Exception>(() => ProtobufEncoder.Decode<MegaMixedMessage>(malformed));
    }

    public static IEnumerable<object[]> GetMalformedPayloads()
    {
        // Truncated length-delimited field.
        yield return [new byte[] { 0x0A, 0x05, 0x01, 0x02 }];

        // Truncated varint.
        yield return [new byte[] { 0x08, 0x80 }];

        // Length-delimited field with absurd length and no payload.
        yield return [new byte[] { 0x0A, 0xFF, 0xFF, 0xFF, 0x7F }];

        // Nested payload claims more bytes than are present.
        yield return [new byte[] { 0x9A, 0x01, 0x04, 0x0A, 0x02, 0x41 }];
    }

    private static MegaMixedMessage CreateMegaMixedMessage(
        int childCount,
        int mapCount,
        int stringLength,
        int byteLength)
    {
        var bytes = new byte[byteLength];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)(i % 251);

        var message = new MegaMixedMessage
        {
            Guid = Guid.NewGuid(),
            Decimal = 123456789.987654321m,
            Int128 = Int128.MaxValue,
            UInt128 = UInt128.MaxValue,
            DateTime = DateTime.UtcNow,
            DateTimeOffset = DateTimeOffset.UtcNow,
            TimeSpan = TimeSpan.FromTicks(long.MaxValue / 2),
            Double = double.MaxValue,
            Float = float.MaxValue,
            Half = (Half)123.45,
            BigInt = BigInteger.Pow(2, 512),
            Complex = new Complex(12.3, 45.6),
            String = new string('Z', stringLength),
            Bytes = bytes,
            Version = new Version(4, 5, 6, 7),
            Uri = new Uri("https://very-long-and-complex-url.com/path?query=123"),
            NInt = (nint)int.MaxValue,
            NUint = (nuint)uint.MaxValue
        };

        for (int i = 0; i < childCount; i++)
            message.Children.Add(new NestedObject { Name = $"Child {i}" });

        for (int i = 0; i < mapCount; i++)
            message.Map[$"Key {i}"] = new NestedObject { Name = $"Map Value {i}" };

        return message;
    }
}
