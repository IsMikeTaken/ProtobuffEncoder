using System.Reflection;
using Grpc.Net.Client;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Grpc.Client;

/// <summary>
/// Extension methods for creating protobuf-encoded gRPC clients from a <see cref="GrpcChannel"/>.
/// </summary>
public static class GrpcChannelExtensions
{
    /// <summary>
    /// Creates a typed gRPC client proxy for the given service interface.
    /// The interface must be decorated with <see cref="ProtoServiceAttribute"/> and its
    /// methods with <see cref="ProtoMethodAttribute"/>.
    /// <para>
    /// The returned proxy dispatches all method calls through the gRPC channel using
    /// <see cref="ProtobufMarshaller"/> for serialization — no .proto files needed.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var channel = GrpcChannel.ForAddress("http://localhost:5400");
    /// var client = channel.CreateProtobufClient&lt;IWeatherGrpcService&gt;();
    /// var response = await client.GetForecast(new WeatherRequest { City = "Amsterdam" });
    /// </code>
    /// </example>
    public static TService CreateProtobufClient<TService>(this GrpcChannel channel)
        where TService : class
    {
        if (!typeof(TService).IsInterface)
            throw new ArgumentException(
                $"{typeof(TService).Name} must be an interface decorated with [ProtoService].",
                nameof(TService));

        if (typeof(TService).GetCustomAttribute<ProtoServiceAttribute>() is null)
            throw new ArgumentException(
                $"{typeof(TService).Name} must be decorated with [ProtoService].",
                nameof(TService));

        var proxy = DispatchProxy.Create<TService, ProtobufGrpcClientProxy>();
        ((ProtobufGrpcClientProxy)(object)proxy).Initialize(channel.CreateCallInvoker(), typeof(TService));
        return proxy;
    }
}
