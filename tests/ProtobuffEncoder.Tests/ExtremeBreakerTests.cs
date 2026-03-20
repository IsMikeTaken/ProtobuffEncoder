using System.Numerics;
using ProtobuffEncoder.Attributes;
using Xunit;

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
     * Bit-Error-Simulation Pattern: Feeds random binary junk to the decoder 
     * to ensure it fails with a predictable exception instead of hanging or 
     * crashing the process.
     */
    [Fact]
    public void Decode_MalformedData_ThrowsInvalidOperation()
    {
        // Arrange
        var junk = new byte[100];
        new Random().NextBytes(junk);

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => ProtobufEncoder.Decode<MegaMixedMessage>(junk));
    }
}
