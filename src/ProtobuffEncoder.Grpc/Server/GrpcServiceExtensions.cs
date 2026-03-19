using Grpc.AspNetCore.Server.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ProtobuffEncoder.Grpc.Server;

/// <summary>
/// Extension methods for registering ProtobuffEncoder-based gRPC services.
/// </summary>
public static class GrpcServiceExtensions
{
    /// <summary>
    /// Registers the gRPC infrastructure and a protobuf-encoded gRPC service.
    /// Call once per service implementation type, then use <c>app.MapGrpcService&lt;TService&gt;()</c>
    /// to expose endpoints.
    /// <para>
    /// The service must implement an interface decorated with <see cref="Attributes.ProtoServiceAttribute"/>,
    /// whose methods are marked with <see cref="Attributes.ProtoMethodAttribute"/>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddGrpc();
    /// builder.Services.AddProtobufGrpcService&lt;WeatherGrpcService&gt;();
    ///
    /// app.MapGrpcService&lt;WeatherGrpcService&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddProtobufGrpcService<TService>(this IServiceCollection services)
        where TService : class
    {
        services.TryAddScoped<TService>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IServiceMethodProvider<TService>,
                ProtobufGrpcServiceMethodProvider<TService>>());

        return services;
    }
}
