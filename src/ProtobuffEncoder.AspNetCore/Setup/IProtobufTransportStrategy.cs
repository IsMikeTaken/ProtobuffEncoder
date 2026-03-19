using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ProtobuffEncoder.AspNetCore.Setup;

/// <summary>
/// Strategy interface for registering a transport with the ProtobuffEncoder framework.
/// <para>
/// Implementations encapsulate the DI setup (<see cref="ConfigureServices"/>) and
/// middleware/endpoint setup (<see cref="ConfigureEndpoints"/>) for a specific transport
/// (REST formatters, WebSocket endpoints, gRPC services, etc.).
/// </para>
/// <para>
/// Register via <c>builder.AddTransport(new MyStrategy())</c> inside the
/// <see cref="ProtobufEncoderBuilder"/> fluent chain, or use the built-in convenience
/// methods (<c>.WithRestFormatters()</c>, <c>.WithWebSocket()</c>, <c>.WithGrpc()</c>).
/// </para>
/// </summary>
public interface IProtobufTransportStrategy
{
    /// <summary>
    /// Registers services in the DI container (called during <c>builder.Services</c> phase).
    /// </summary>
    void ConfigureServices(IServiceCollection services, ProtobufEncoderOptions options);

    /// <summary>
    /// Maps endpoints and middleware (called during <c>app</c> phase).
    /// Implementations that only register DI services can leave this empty.
    /// </summary>
    void ConfigureEndpoints(IEndpointRouteBuilder endpoints, ProtobufEncoderOptions options);
}
