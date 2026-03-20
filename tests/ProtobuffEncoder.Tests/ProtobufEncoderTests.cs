using FakeItEasy;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Tests;

/// <summary>
/// Tests for ProtobufEncoder.Encode / Decode covering every scalar type,
/// nested messages, enums, nullables, and edge cases.
/// All tests follow AAA (Arrange, Act, Assert).
/// </summary>
public class ProtobufEncoderTests
{
    [Fact]
    public void Encode_Decode_SimpleMessage_RoundTrips()
    {
        // Arrange
        var original = new SimpleMessage { Id = 42, Name = "Hello", IsActive = true };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<SimpleMessage>(bytes);

        // Assert
        Assert.Equal(42, decoded.Id);
        Assert.Equal("Hello", decoded.Name);
        Assert.True(decoded.IsActive);
    }

    [Fact]
    public void Encode_Decode_AllScalars_RoundTrips()
    {
        // Arrange
        var original = new AllScalarsMessage
        {
            Flag = true,
            ByteValue = 255,
            SByteValue = -128,
            ShortValue = -32768,
            UShortValue = 65535,
            IntValue = int.MinValue,
            UIntValue = uint.MaxValue,
            LongValue = long.MinValue,
            ULongValue = ulong.MaxValue,
            FloatValue = 3.14f,
            DoubleValue = 2.718281828,
            StringValue = "test-string",
            ByteArrayValue = [0x00, 0xFF, 0xAB]
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<AllScalarsMessage>(bytes);

        // Assert
        Assert.True(decoded.Flag);
        Assert.Equal(255, decoded.ByteValue);
        Assert.Equal(-128, decoded.SByteValue);
        Assert.Equal(-32768, decoded.ShortValue);
        Assert.Equal(65535, decoded.UShortValue);
        Assert.Equal(int.MinValue, decoded.IntValue);
        Assert.Equal(uint.MaxValue, decoded.UIntValue);
        Assert.Equal(long.MinValue, decoded.LongValue);
        Assert.Equal(ulong.MaxValue, decoded.ULongValue);
        Assert.Equal(3.14f, decoded.FloatValue);
        Assert.Equal(2.718281828, decoded.DoubleValue);
        Assert.Equal("test-string", decoded.StringValue);
        Assert.Equal([0x00, 0xFF, 0xAB], decoded.ByteArrayValue);
    }

    [Fact]
    public void Encode_Decode_ZeroValues_SkipsDefaults()
    {
        // Arrange
        var original = new AllScalarsMessage();

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<AllScalarsMessage>(bytes);

        // Assert
        Assert.False(decoded.Flag);
        Assert.Equal(0, decoded.IntValue);
        Assert.Equal(0u, decoded.UIntValue);
        Assert.Equal(0L, decoded.LongValue);
        Assert.Equal(0UL, decoded.ULongValue);
        Assert.Equal(0f, decoded.FloatValue);
        Assert.Equal(0d, decoded.DoubleValue);
        Assert.Equal("", decoded.StringValue);
    }

    [Fact]
    public void Encode_Decode_Enum_RoundTrips()
    {
        // Arrange
        var original = new EnumMessage { Priority = Priority.Critical, Label = "urgent" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<EnumMessage>(bytes);

        // Assert
        Assert.Equal(Priority.Critical, decoded.Priority);
        Assert.Equal("urgent", decoded.Label);
    }

    [Fact]
    public void Encode_Decode_Enum_DefaultValue_RoundTrips()
    {
        // Arrange
        var original = new EnumMessage { Priority = Priority.Low, Label = "low" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<EnumMessage>(bytes);

        // Assert
        Assert.Equal(Priority.Low, decoded.Priority);
        Assert.Equal("low", decoded.Label);
    }

    [Fact]
    public void Encode_Decode_NullableWithValues_RoundTrips()
    {
        // Arrange
        var original = new NullableMessage
        {
            NullableInt = 99,
            NullableBool = true,
            NullableDouble = 1.5
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<NullableMessage>(bytes);

        // Assert
        Assert.Equal(99, decoded.NullableInt);
        Assert.True(decoded.NullableBool);
        Assert.Equal(1.5, decoded.NullableDouble);
    }

    [Fact]
    public void Encode_Decode_NullableWithNulls_RoundTrips()
    {
        // Arrange
        var original = new NullableMessage();

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<NullableMessage>(bytes);

        // Assert
        Assert.Null(decoded.NullableInt);
        Assert.Null(decoded.NullableBool);
        Assert.Null(decoded.NullableDouble);
    }

    [Fact]
    public void Encode_Decode_NestedMessage_RoundTrips()
    {
        // Arrange
        var original = new NestedOuter
        {
            Title = "parent",
            Inner = new NestedInner { Value = 77, Detail = "child" }
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<NestedOuter>(bytes);

        // Assert
        Assert.Equal("parent", decoded.Title);
        Assert.Equal(77, decoded.Inner.Value);
        Assert.Equal("child", decoded.Inner.Detail);
    }

    [Fact]
    public void Encode_Decode_DeepNesting_ThreeLevels_RoundTrips()
    {
        // Arrange
        var original = new DeepNested
        {
            Level = "L1",
            Outer = new NestedOuter
            {
                Title = "L2",
                Inner = new NestedInner { Value = 42, Detail = "L3" }
            }
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<DeepNested>(bytes);

        // Assert
        Assert.Equal("L1", decoded.Level);
        Assert.Equal("L2", decoded.Outer.Title);
        Assert.Equal(42, decoded.Outer.Inner.Value);
        Assert.Equal("L3", decoded.Outer.Inner.Detail);
    }

    [Fact]
    public void Encode_WithWriteDefault_WritesZeroValues()
    {
        // Arrange
        var original = new WriteDefaultMessage { AlwaysWritten = 0, SkippedWhenDefault = 0 };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<WriteDefaultMessage>(bytes);

        // Assert
        Assert.NotEmpty(bytes);
        Assert.Equal(0, decoded.AlwaysWritten);
    }

    [Fact]
    public void Encode_RequiredFieldEmpty_Throws()
    {
        // Arrange — MustHaveValue is "" which is default for string
        var message = new RequiredFieldMessage { MustHaveValue = "", Optional = 5 };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => ProtobufEncoder.Encode(message));
        Assert.Contains("Required field", ex.Message);
        Assert.Contains("MustHaveValue", ex.Message);
    }

    [Fact]
    public void Encode_RequiredFieldPresent_Succeeds()
    {
        // Arrange
        var message = new RequiredFieldMessage { MustHaveValue = "ok", Optional = 5 };

        // Act
        var bytes = ProtobufEncoder.Encode(message);
        var decoded = ProtobufEncoder.Decode<RequiredFieldMessage>(bytes);

        // Assert
        Assert.Equal("ok", decoded.MustHaveValue);
        Assert.Equal(5, decoded.Optional);
    }

    [Fact]
    public void Encode_IgnoredField_IsNotSerialized()
    {
        // Arrange
        var original = new IgnoredFieldMessage { Visible = "seen", Hidden = "secret" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<IgnoredFieldMessage>(bytes);

        // Assert
        Assert.Equal("seen", decoded.Visible);
        Assert.Equal("", decoded.Hidden); // default, not serialized
    }

    [Fact]
    public void Encode_ExplicitFields_OnlyMarkedPropertiesSerialized()
    {
        // Arrange
        var original = new ExplicitMessage { Included = 10, Excluded = "gone", AlsoIncluded = "here" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ExplicitMessage>(bytes);

        // Assert
        Assert.Equal(10, decoded.Included);
        Assert.Equal("", decoded.Excluded); // not serialized
        Assert.Equal("here", decoded.AlsoIncluded);
    }

    [Fact]
    public void Encode_NullInstance_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ProtobufEncoder.Encode(null!));
    }

    [Fact]
    public void Decode_EmptyData_ReturnsDefaultInstance()
    {
        // Arrange
        ReadOnlySpan<byte> empty = [];

        // Act
        var decoded = ProtobufEncoder.Decode<SimpleMessage>(empty);

        // Assert
        Assert.Equal(0, decoded.Id);
        Assert.Equal("", decoded.Name);
        Assert.False(decoded.IsActive);
    }

    [Fact]
    public void Encode_Decode_ImplicitNestedType_RoundTrips()
    {
        // Arrange
        var original = new ImplicitParent
        {
            Title = "parent",
            Child = new ImplicitChild { Value = 55, Label = "implicitly serialized" }
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<ImplicitParent>(bytes);

        // Assert
        Assert.Equal("parent", decoded.Title);
        Assert.Equal(55, decoded.Child.Value);
        Assert.Equal("implicitly serialized", decoded.Child.Label);
    }

    [Fact]
    public void Encode_Decode_LargeString_RoundTrips()
    {
        // Arrange
        var largeString = new string('X', 100_000);
        var original = new SimpleMessage { Id = 1, Name = largeString };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<SimpleMessage>(bytes);

        // Assert
        Assert.Equal(largeString, decoded.Name);
    }

    [Fact]
    public void Encode_ToStream_ProducesIdenticalBytes()
    {
        // Arrange
        var msg = new SimpleMessage { Id = 7, Name = "stream" };
        using var stream = new MemoryStream();

        // Act
        ProtobufEncoder.Encode(msg, stream);
        var streamBytes = stream.ToArray();
        var directBytes = ProtobufEncoder.Encode(msg);

        // Assert
        Assert.Equal(directBytes, streamBytes);
    }

    [Fact]
    public void Encode_ToNullStream_ThrowsArgumentNull()
    {
        // Arrange
        var msg = new SimpleMessage { Id = 1 };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ProtobufEncoder.Encode(msg, null!));
    }

    [Fact]
    public async Task EncodeAsync_WritesToStream()
    {
        // Arrange
        var msg = new SimpleMessage { Id = 42, Name = "async" };
        using var stream = new MemoryStream();

        // Act
        await ProtobufEncoder.EncodeAsync(msg, stream);
        stream.Position = 0;
        var decoded = await ProtobufEncoder.DecodeAsync<SimpleMessage>(stream);

        // Assert
        Assert.Equal(42, decoded.Id);
        Assert.Equal("async", decoded.Name);
    }

    [Fact]
    public async Task EncodeAsync_NullInstance_Throws()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ProtobufEncoder.EncodeAsync(null!, stream));
    }

    [Fact]
    public async Task EncodeAsync_NullStream_Throws()
    {
        // Arrange
        var msg = new SimpleMessage();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ProtobufEncoder.EncodeAsync(msg, null!));
    }

    [Fact]
    public async Task Encode_Decode_ConcurrentCalls_AllSucceed()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
        {
            var msg = new SimpleMessage { Id = i, Name = $"msg-{i}" };
            var bytes = ProtobufEncoder.Encode(msg);
            var decoded = ProtobufEncoder.Decode<SimpleMessage>(bytes);
            Assert.Equal(i, decoded.Id);
            Assert.Equal($"msg-{i}", decoded.Name);
        }));

        // Act & Assert — no exceptions from concurrent usage
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Encode_Decode_ConcurrentDifferentTypes_AllSucceed()
    {
        // Arrange
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            int idx = i;
            tasks.Add(Task.Run(() =>
            {
                var msg = new EnumMessage { Priority = (Priority)(idx % 4), Label = $"label-{idx}" };
                var bytes = ProtobufEncoder.Encode(msg);
                var decoded = ProtobufEncoder.Decode<EnumMessage>(bytes);
                Assert.Equal((Priority)(idx % 4), decoded.Priority);
            }));
            tasks.Add(Task.Run(() =>
            {
                var msg = new NestedOuter { Title = $"nested-{idx}", Inner = new NestedInner { Value = idx } };
                var bytes = ProtobufEncoder.Encode(msg);
                var decoded = ProtobufEncoder.Decode<NestedOuter>(bytes);
                Assert.Equal($"nested-{idx}", decoded.Title);
                Assert.Equal(idx, decoded.Inner.Value);
            }));
        }

        // Act & Assert
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void Encode_Decode_MaxIntValues_RoundTrips()
    {
        // Arrange
        var original = new AllScalarsMessage
        {
            IntValue = int.MaxValue,
            UIntValue = uint.MaxValue,
            LongValue = long.MaxValue,
            ULongValue = ulong.MaxValue
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<AllScalarsMessage>(bytes);

        // Assert
        Assert.Equal(int.MaxValue, decoded.IntValue);
        Assert.Equal(uint.MaxValue, decoded.UIntValue);
        Assert.Equal(long.MaxValue, decoded.LongValue);
        Assert.Equal(ulong.MaxValue, decoded.ULongValue);
    }

    [Fact]
    public void Encode_Decode_MinIntValues_RoundTrips()
    {
        // Arrange
        var original = new AllScalarsMessage
        {
            IntValue = int.MinValue,
            LongValue = long.MinValue,
            SByteValue = sbyte.MinValue,
            ShortValue = short.MinValue
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<AllScalarsMessage>(bytes);

        // Assert
        Assert.Equal(int.MinValue, decoded.IntValue);
        Assert.Equal(long.MinValue, decoded.LongValue);
        Assert.Equal(sbyte.MinValue, decoded.SByteValue);
        Assert.Equal(short.MinValue, decoded.ShortValue);
    }

    [Fact]
    public void Encode_Decode_FloatExtremes_RoundTrips()
    {
        // Arrange
        var original = new AllScalarsMessage
        {
            FloatValue = float.MaxValue,
            DoubleValue = double.MinValue
        };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<AllScalarsMessage>(bytes);

        // Assert
        Assert.Equal(float.MaxValue, decoded.FloatValue);
        Assert.Equal(double.MinValue, decoded.DoubleValue);
    }

    [Fact]
    public void Encode_Decode_UnicodeString_RoundTrips()
    {
        // Arrange
        var original = new SimpleMessage { Id = 1, Name = "日本語テスト 🎉 émojis & ñ" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<SimpleMessage>(bytes);

        // Assert
        Assert.Equal("日本語テスト 🎉 émojis & ñ", decoded.Name);
    }

    [Fact]
    public void Encode_Decode_EmptyString_RoundTrips()
    {
        // Arrange
        var original = new SimpleMessage { Id = 1, Name = "" };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<SimpleMessage>(bytes);

        // Assert
        Assert.Equal("", decoded.Name);
    }

    [Fact]
    public void Encode_Decode_LargeByteArray_RoundTrips()
    {
        // Arrange
        var largeBytes = new byte[50_000];
        Random.Shared.NextBytes(largeBytes);
        var original = new AllScalarsMessage { ByteArrayValue = largeBytes };

        // Act
        var bytes = ProtobufEncoder.Encode(original);
        var decoded = ProtobufEncoder.Decode<AllScalarsMessage>(bytes);

        // Assert
        Assert.Equal(largeBytes, decoded.ByteArrayValue);
    }
}
