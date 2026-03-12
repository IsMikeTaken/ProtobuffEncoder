using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// DI registration extensions for protobuf WebSocket services.
/// </summary>
public static class WebSocketServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="WebSocketConnectionManager{TSend, TReceive}"/> as a singleton
    /// for the given message type pair. Call once per endpoint type pair.
    /// <para>
    /// The connection manager tracks all active WebSocket connections and supports
    /// broadcast to all connected clients.
    /// </para>
    /// </summary>
    public static IServiceCollection AddProtobufWebSocketEndpoint<TSend, TReceive>(
        this IServiceCollection services)
        where TSend : class, new()
        where TReceive : class, new()
    {
        services.TryAddSingleton<WebSocketConnectionManager<TSend, TReceive>>();
        return services;
    }
}
