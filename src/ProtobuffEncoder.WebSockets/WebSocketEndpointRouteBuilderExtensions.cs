using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProtobuffEncoder.Transport;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// Extension methods for registering protobuf WebSocket endpoints on ASP.NET Core routing.
/// </summary>
public static class WebSocketEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a protobuf WebSocket endpoint at the given route pattern.
    /// <para>
    /// Each connecting client gets a <see cref="ProtobufWebSocketConnection{TSend, TReceive}"/>
    /// with lifecycle hooks (OnConnect, OnMessage, OnDisconnect, OnError) and automatic
    /// connection tracking via <see cref="WebSocketConnectionManager{TSend, TReceive}"/>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// app.MapProtobufWebSocket&lt;NotificationMessage, NotificationMessage&gt;("/ws/chat", options =>
    /// {
    ///     options.OnConnect = async conn => { /* welcome */ };
    ///     options.OnMessage = async (conn, msg) => { await conn.SendAsync(reply); };
    /// });
    /// </code>
    /// </example>
    public static IEndpointConventionBuilder MapProtobufWebSocket<TSend, TReceive>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Action<ProtobufWebSocketOptions<TSend, TReceive>> configure)
        where TSend : class, new()
        where TReceive : class, new()
    {
        var options = new ProtobufWebSocketOptions<TSend, TReceive>();
        configure(options);
        return MapProtobufWebSocket(endpoints, pattern, options);
    }

    /// <summary>
    /// Maps a protobuf WebSocket endpoint with a pre-built options object.
    /// </summary>
    public static IEndpointConventionBuilder MapProtobufWebSocket<TSend, TReceive>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        ProtobufWebSocketOptions<TSend, TReceive> options)
        where TSend : class, new()
        where TReceive : class, new()
    {
        return endpoints.MapGet(pattern, async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                return;
            }

            var logger = ctx.RequestServices.GetService<ILoggerFactory>()
                ?.CreateLogger($"ProtobufWS[{pattern}]");

            var manager = ctx.RequestServices.GetRequiredService<WebSocketConnectionManager<TSend, TReceive>>();

            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            var connectionId = Guid.NewGuid().ToString("N")[..12];

            await using var connection = new ProtobufWebSocketConnection<TSend, TReceive>(ws, connectionId);
            manager.Add(connection);

            logger?.LogInformation("Client {ConnectionId} connected to {Pattern} ({Count} total)",
                connectionId, pattern, manager.Count);

            // Build validation pipelines
            ValidationPipeline<TSend>? sendValidation = null;
            ValidationPipeline<TReceive>? receiveValidation = null;

            if (options.ConfigureSendValidation is not null)
            {
                sendValidation = new ValidationPipeline<TSend>();
                options.ConfigureSendValidation(sendValidation);
            }

            if (options.ConfigureReceiveValidation is not null)
            {
                receiveValidation = new ValidationPipeline<TReceive>();
                options.ConfigureReceiveValidation(receiveValidation);
            }

            try
            {
                // OnConnect lifecycle hook
                if (options.OnConnect is not null)
                    await options.OnConnect(connection);

                // Message loop
                await foreach (var message in connection.ReceiveAllAsync(ctx.RequestAborted))
                {
                    // Receive validation
                    if (receiveValidation is { HasValidators: true })
                    {
                        var result = receiveValidation.Validate(message);
                        if (!result.IsValid)
                        {
                            switch (options.OnInvalidReceive)
                            {
                                case InvalidMessageBehavior.Throw:
                                    throw new MessageValidationException(result.ErrorMessage!, message);

                                case InvalidMessageBehavior.Skip:
                                    if (options.OnMessageRejected is not null)
                                        await options.OnMessageRejected(connection, message, result);
                                    continue;

                                case InvalidMessageBehavior.ReturnNull:
                                    if (options.OnMessageRejected is not null)
                                        await options.OnMessageRejected(connection, message, result);
                                    return;
                            }
                        }
                    }

                    if (options.OnMessage is not null)
                        await options.OnMessage(connection, message);
                }
            }
            catch (OperationCanceledException) { /* client disconnected */ }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error on connection {ConnectionId}", connectionId);
                if (options.OnError is not null)
                    await options.OnError(connection, ex);
            }
            finally
            {
                manager.Remove(connectionId);

                logger?.LogInformation("Client {ConnectionId} disconnected from {Pattern} ({Count} remaining)",
                    connectionId, pattern, manager.Count);

                if (options.OnDisconnect is not null)
                {
                    try { await options.OnDisconnect(connection); }
                    catch { /* best-effort */ }
                }
            }
        });
    }
}
