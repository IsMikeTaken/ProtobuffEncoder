using System.Numerics;
using ProtobuffEncoder.Attributes;
using Xunit;

namespace ProtobuffEncoder.Tests;

public class CommonTypesTests
{
    [ProtoContract]
    public class AllTypesMessage
    {
        [ProtoField(1)] public Guid GuidValue { get; set; }
        [ProtoField(2)] public decimal DecimalValue { get; set; }
        [ProtoField(3)] public DateTime DateTimeValue { get; set; }
        [ProtoField(4)] public DateTimeOffset DateTimeOffsetValue { get; set; }
        [ProtoField(5)] public TimeSpan TimeSpanValue { get; set; }
        [ProtoField(6)] public DateOnly DateOnlyValue { get; set; }
        [ProtoField(7)] public TimeOnly TimeOnlyValue { get; set; }
        [ProtoField(8)] public Int128 Int128Value { get; set; }
        [ProtoField(9)] public UInt128 UInt128Value { get; set; }
        [ProtoField(10)] public Half HalfValue { get; set; }
        [ProtoField(11)] public BigInteger BigIntValue { get; set; }
        [ProtoField(12)] public Complex ComplexValue { get; set; }
        [ProtoField(13)] public Version VersionValue { get; set; } = new(1, 0);
        [ProtoField(14)] public Uri UriValue { get; set; } = new("http://localhost");
        [ProtoField(15)] public nint NIntValue { get; set; }
        [ProtoField(16)] public nuint NUintValue { get; set; }
    }

    [ProtoContract]
    public class NullableTypesMessage
    {
        [ProtoField(1)] public Guid? GuidValue { get; set; }
        [ProtoField(2)] public decimal? DecimalValue { get; set; }
        [ProtoField(3)] public DateTime? DateTimeValue { get; set; }
        [ProtoField(4)] public nint? NIntValue { get; set; }
    }

    [Fact]
    public void Roundtrip_AllCommonAndUncommonTypes()
    {
        // Arrange
        var original = new AllTypesMessage
        {
            GuidValue = Guid.NewGuid(),
            DecimalValue = 1234.5678m,
            DateTimeValue = DateTime.UtcNow,
            DateTimeOffsetValue = DateTimeOffset.Now,
            TimeSpanValue = TimeSpan.FromMinutes(123),
            DateOnlyValue = DateOnly.FromDateTime(DateTime.Today),
            TimeOnlyValue = TimeOnly.FromDateTime(DateTime.Now),
            Int128Value = Int128.MaxValue,
            UInt128Value = UInt128.MaxValue,
            HalfValue = (Half)3.14,
            BigIntValue = BigInteger.Parse("123456789012345678901234567890"),
            ComplexValue = new Complex(1.5, -2.5),
            VersionValue = new Version(2, 1, 0, 5),
            UriValue = new Uri("https://google.com/search?q=protobuf"),
            NIntValue = (nint)123,
            NUintValue = (nuint)456
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<AllTypesMessage>(bytes);

        // Assert
        Assert.Equal(original.GuidValue, decoded.GuidValue);
        Assert.Equal(original.DecimalValue, decoded.DecimalValue);
        Assert.Equal(original.DateTimeValue.Ticks, decoded.DateTimeValue.Ticks);
        Assert.Equal(original.DateTimeOffsetValue, decoded.DateTimeOffsetValue);
        Assert.Equal(original.TimeSpanValue, decoded.TimeSpanValue);
        Assert.Equal(original.DateOnlyValue, decoded.DateOnlyValue);
        Assert.Equal(original.TimeOnlyValue.Ticks, decoded.TimeOnlyValue.Ticks);
        Assert.Equal(original.Int128Value, decoded.Int128Value);
        Assert.Equal(original.UInt128Value, decoded.UInt128Value);
        Assert.Equal(original.HalfValue, decoded.HalfValue);
        Assert.Equal(original.BigIntValue, decoded.BigIntValue);
        Assert.Equal(original.ComplexValue, decoded.ComplexValue);
        Assert.Equal(original.VersionValue, decoded.VersionValue);
        Assert.Equal(original.UriValue, decoded.UriValue);
        Assert.Equal(original.NIntValue, decoded.NIntValue);
        Assert.Equal(original.NUintValue, decoded.NUintValue);
    }

    [Fact]
    public void Roundtrip_NullableTypes_WithValues()
    {
        // Arrange
        var original = new NullableTypesMessage
        {
            GuidValue = Guid.NewGuid(),
            DecimalValue = 99.99m,
            DateTimeValue = DateTime.Now,
            NIntValue = (nint)789
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<NullableTypesMessage>(bytes);

        // Assert
        Assert.Equal(original.GuidValue, decoded.GuidValue);
        Assert.Equal(original.DecimalValue, decoded.DecimalValue);
        Assert.Equal(original.DateTimeValue.Value.Ticks, decoded.DateTimeValue.Value.Ticks);
        Assert.Equal(original.NIntValue, decoded.NIntValue);
    }

    [Fact]
    public void Roundtrip_NullableTypes_Null()
    {
        // Arrange
        var original = new NullableTypesMessage
        {
            GuidValue = null,
            DecimalValue = null,
            DateTimeValue = null,
            NIntValue = null
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<NullableTypesMessage>(bytes);

        // Assert
        Assert.Null(decoded.GuidValue);
        Assert.Null(decoded.DecimalValue);
        Assert.Null(decoded.DateTimeValue);
        Assert.Null(decoded.NIntValue);
    }
}
