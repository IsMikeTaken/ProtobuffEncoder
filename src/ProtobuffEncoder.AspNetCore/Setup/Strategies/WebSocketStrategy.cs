using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ProtobuffEncoder.AspNetCore.Setup.Strategies;

/// <summary>
/// Registers protobuf WebSocket connection managers for the given type pairs.
/// Endpoints are mapped separately via <c>app.MapProtobufWebSocket()</c>.
/// <para>
/// Each call to <see cref="AddEndpoint{TSend, TReceive}"/> registers a
/// <c>WebSocketConnectionManager&lt;TSend, TReceive&gt;</c> singleton for broadcast support.
/// </para>
/// </summary>
public sealed class WebSocketStrategy : IProtobufTransportStrategy
{
    private readonly List<Action<IServiceCollection>> _registrations = [];

    /// <summary>
    /// Registers a WebSocket endpoint type pair for connection tracking and broadcast.
    /// </summary>
    public WebSocketStrategy AddEndpoint<TSend, TReceive>()
        where TSend : class, new()
        where TReceive : class, new()
    {
        _registrations.Add(services =>
            WebSockets.WebSocketServiceCollectionExtensions.AddProtobufWebSocketEndpoint<TSend, TReceive>(services));
        return this;
    }

    public void ConfigureServices(IServiceCollection services, ProtobufEncoderOptions options)
    {
        foreach (var registration in _registrations)
            registration(services);
    }

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints, ProtobufEncoderOptions options) { }
}
