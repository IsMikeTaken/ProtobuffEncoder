using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Grpc.Tests.Fixtures;

namespace ProtobuffEncoder.Grpc.Tests;

/// <summary>
/// Tests for <see cref="ServiceMethodDescriptor"/> — gRPC method discovery via reflection.
/// </summary>
public class ServiceMethodDescriptorTests
{
    #region Simple-Test Pattern — discovery from implementation type

    [Fact]
    public void Discover_PingServiceImpl_FindsOneMethod()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(PingServiceImpl));

        Assert.Single(descriptors);
        Assert.Equal("PingService", descriptors[0].ServiceName);
        Assert.Equal("Ping", descriptors[0].MethodName);
        Assert.Equal(ProtoMethodType.Unary, descriptors[0].MethodType);
    }

    [Fact]
    public void Discover_PingServiceImpl_ExtractsRequestResponseTypes()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(PingServiceImpl));

        Assert.Equal(typeof(PingRequest), descriptors[0].RequestType);
        Assert.Equal(typeof(PingResponse), descriptors[0].ResponseType);
    }

    [Fact]
    public void Discover_PingServiceImpl_DetectsCancellationToken()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(PingServiceImpl));

        Assert.True(descriptors[0].HasCancellationToken);
    }

    #endregion

    #region Code-Path Pattern — all four method types

    [Fact]
    public void Discover_StreamServiceImpl_FindsAllFourMethodTypes()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(StreamServiceImpl));

        Assert.Equal(4, descriptors.Count);
        Assert.Contains(descriptors, d => d.MethodType == ProtoMethodType.Unary);
        Assert.Contains(descriptors, d => d.MethodType == ProtoMethodType.ServerStreaming);
        Assert.Contains(descriptors, d => d.MethodType == ProtoMethodType.ClientStreaming);
        Assert.Contains(descriptors, d => d.MethodType == ProtoMethodType.DuplexStreaming);
    }

    [Fact]
    public void Discover_UnaryMethod_ExtractsCorrectTypes()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(StreamServiceImpl));
        var echo = descriptors.First(d => d.MethodName == "Echo");

        Assert.Equal(ProtoMethodType.Unary, echo.MethodType);
        Assert.Equal(typeof(PingRequest), echo.RequestType);
        Assert.Equal(typeof(PingResponse), echo.ResponseType);
        Assert.False(echo.HasCancellationToken);
    }

    [Fact]
    public void Discover_ServerStreamingMethod_ExtractsCorrectTypes()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(StreamServiceImpl));
        var stream = descriptors.First(d => d.MethodName == "GetStream");

        Assert.Equal(ProtoMethodType.ServerStreaming, stream.MethodType);
        Assert.Equal(typeof(PingRequest), stream.RequestType);
        Assert.Equal(typeof(StreamItem), stream.ResponseType);
        Assert.True(stream.HasCancellationToken);
    }

    [Fact]
    public void Discover_ClientStreamingMethod_ExtractsCorrectTypes()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(StreamServiceImpl));
        var agg = descriptors.First(d => d.MethodName == "Aggregate");

        Assert.Equal(ProtoMethodType.ClientStreaming, agg.MethodType);
        Assert.Equal(typeof(StreamItem), agg.RequestType);
        Assert.Equal(typeof(AggregateResult), agg.ResponseType);
    }

    [Fact]
    public void Discover_DuplexStreamingMethod_ExtractsCorrectTypes()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(StreamServiceImpl));
        var duplex = descriptors.First(d => d.MethodName == "BiDirectional");

        Assert.Equal(ProtoMethodType.DuplexStreaming, duplex.MethodType);
        Assert.Equal(typeof(StreamItem), duplex.RequestType);
        Assert.Equal(typeof(StreamItem), duplex.ResponseType);
    }

    #endregion

    #region Process-Rule Pattern — interface-only discovery

    [Fact]
    public void Discover_InterfaceOnly_FindsMethods()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(IPingService), isInterfaceOnly: true);

        Assert.Single(descriptors);
        Assert.Equal("Ping", descriptors[0].MethodName);
        Assert.Equal(ProtoMethodType.Unary, descriptors[0].MethodType);
    }

    [Fact]
    public void Discover_InterfaceOnlyAllTypes_FindsAllMethods()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(IStreamService), isInterfaceOnly: true);

        Assert.Equal(4, descriptors.Count);
    }

    #endregion

    #region Constraint-Data Pattern — edge cases

    [Fact]
    public void Discover_TypeWithoutProtoService_ReturnsEmpty()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(string));

        Assert.Empty(descriptors);
    }

    [Fact]
    public void Discover_InterfaceWithoutAttribute_ReturnsEmpty()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(INotAService), isInterfaceOnly: true);

        Assert.Empty(descriptors);
    }

    #endregion

    #region Collection-Indexing Pattern — method name lookup

    [Fact]
    public void Discover_MethodNames_MatchInterfaceMethodNames()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(StreamServiceImpl));

        var names = descriptors.Select(d => d.MethodName).OrderBy(n => n).ToList();
        Assert.Contains("Aggregate", names);
        Assert.Contains("BiDirectional", names);
        Assert.Contains("Echo", names);
        Assert.Contains("GetStream", names);
    }

    [Fact]
    public void Discover_ServiceName_MatchesAttributeValue()
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(StreamServiceImpl));

        Assert.All(descriptors, d => Assert.Equal("StreamService", d.ServiceName));
    }

    #endregion
}
