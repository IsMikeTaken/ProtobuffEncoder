using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ProtobuffEncoder.AspNetCore.Setup;

/// <summary>
/// Extension methods for mapping ProtobuffEncoder endpoints during the <c>app</c> pipeline phase.
/// </summary>
public static class ProtobufEncoderEndpointExtensions
{
    /// <summary>
    /// Maps all auto-mapped endpoints registered via the <see cref="ProtobufEncoderBuilder"/>
    /// (e.g., gRPC services registered with <c>autoMap: true</c>).
    /// <para>
    /// Call this in your <c>app</c> pipeline after <c>UseRouting()</c>:
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.UseWebSockets();
    /// app.MapProtobufEndpoints();          // maps auto-mapped gRPC services
    /// app.MapProtobufWebSocket(...);       // manually map WebSocket endpoints
    /// app.Run();
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapProtobufEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var encoderBuilder = endpoints.ServiceProvider.GetService<ProtobufEncoderBuilder>();

        if (encoderBuilder is not null)
            encoderBuilder.MapEndpoints(endpoints);

        return endpoints;
    }
}
