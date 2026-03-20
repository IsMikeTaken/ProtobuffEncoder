using Grpc.Core;
using ProtobuffEncoder.Grpc.Tests.Fixtures;

namespace ProtobuffEncoder.Grpc.Tests;

/// <summary>
/// Tests for <see cref="ProtobufMarshaller"/> — gRPC marshaller creation and serialization.
/// </summary>
public class ProtobufMarshallerTests
{
    #region Simple-Test Pattern — marshaller creation

    [Fact]
    public void Create_ReturnsNonNullMarshaller()
    {
        var marshaller = ProtobufMarshaller.Create<PingRequest>();
        Assert.NotNull(marshaller);
    }

    [Fact]
    public void Create_SerializerIsNotNull()
    {
        var marshaller = ProtobufMarshaller.Create<PingRequest>();
        Assert.NotNull(marshaller.Serializer);
    }

    [Fact]
    public void Create_DeserializerIsNotNull()
    {
        var marshaller = ProtobufMarshaller.Create<PingRequest>();
        Assert.NotNull(marshaller.Deserializer);
    }

    #endregion

    #region Simple-Data-I/O Pattern — round-trip

    [Fact]
    public void Marshaller_RoundTrip_StringField()
    {
        var marshaller = ProtobufMarshaller.Create<PingRequest>();
        var original = new PingRequest { Message = "hello gRPC" };

        var bytes = marshaller.Serializer(original);
        var decoded = marshaller.Deserializer(bytes);

        Assert.Equal("hello gRPC", decoded.Message);
    }

    [Fact]
    public void Marshaller_RoundTrip_MultipleFields()
    {
        var marshaller = ProtobufMarshaller.Create<PingResponse>();
        var original = new PingResponse { Reply = "pong", Timestamp = 1234567890 };

        var bytes = marshaller.Serializer(original);
        var decoded = marshaller.Deserializer(bytes);

        Assert.Equal("pong", decoded.Reply);
        Assert.Equal(1234567890, decoded.Timestamp);
    }

    [Fact]
    public void Marshaller_RoundTrip_ComplexType()
    {
        var marshaller = ProtobufMarshaller.Create<StreamItem>();
        var original = new StreamItem { Sequence = 42, Data = "complex data" };

        var bytes = marshaller.Serializer(original);
        var decoded = marshaller.Deserializer(bytes);

        Assert.Equal(42, decoded.Sequence);
        Assert.Equal("complex data", decoded.Data);
    }

    #endregion

    #region Constraint-Data Pattern — edge cases

    [Fact]
    public void Marshaller_EmptyMessage_RoundTrips()
    {
        var marshaller = ProtobufMarshaller.Create<PingRequest>();
        var original = new PingRequest(); // default values

        var bytes = marshaller.Serializer(original);
        var decoded = marshaller.Deserializer(bytes);

        Assert.Equal("", decoded.Message);
    }

    [Fact]
    public void Marshaller_LargePayload_RoundTrips()
    {
        var marshaller = ProtobufMarshaller.Create<PingRequest>();
        var original = new PingRequest { Message = new string('X', 100_000) };

        var bytes = marshaller.Serializer(original);
        var decoded = marshaller.Deserializer(bytes);

        Assert.Equal(100_000, decoded.Message.Length);
    }

    #endregion

    #region Performance-Test Pattern

    [Fact]
    public void Marshaller_HighVolume_SerializeDeserialize()
    {
        var marshaller = ProtobufMarshaller.Create<PingRequest>();
        var original = new PingRequest { Message = "perf test" };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
        {
            var bytes = marshaller.Serializer(original);
            marshaller.Deserializer(bytes);
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"10k round-trips took {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Code-Path Pattern — different marshallers are independent

    [Fact]
    public void Create_DifferentTypes_ProduceDifferentMarshallers()
    {
        var m1 = ProtobufMarshaller.Create<PingRequest>();
        var m2 = ProtobufMarshaller.Create<PingResponse>();

        var reqBytes = m1.Serializer(new PingRequest { Message = "test" });
        var respBytes = m2.Serializer(new PingResponse { Reply = "test", Timestamp = 1 });

        // Different types, different encodings
        Assert.NotEqual(reqBytes, respBytes);
    }

    #endregion
}
