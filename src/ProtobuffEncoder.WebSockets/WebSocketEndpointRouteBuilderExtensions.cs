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
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each connecting client gets a <see cref="ProtobufWebSocketConnection{TSend,TReceive}"/>
    /// scoped to the request lifetime, with full lifecycle hooks (OnConnect, OnMessage,
    /// OnDisconnect, OnError) and automatic connection tracking via
    /// <see cref="WebSocketConnectionManager{TSend,TReceive}"/>.
    /// </para>
    /// <para>
    /// The endpoint uses <see cref="WebSocketAcceptContext"/> to configure the keep-alive
    /// ping interval from <see cref="ProtobufWebSocketOptions{TSend,TReceive}.KeepAliveInterval"/>,
    /// and calls <see cref="IEndpointConventionBuilder"/> extensions (e.g.
    /// <c>WithDisplayName</c>, <c>DisableRequestTimeout</c>) so the connection is not
    /// terminated by the ASP.NET Core request-timeout middleware.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// app.MapProtobufWebSocket&lt;NotificationMessage, NotificationMessage&gt;("/ws/chat", options =>
    /// {
    ///     options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    ///     options.OnConnect = async conn => { /* send welcome */ };
    ///     options.OnMessage = async (conn, msg) => { await conn.SendAsync(reply); };
    /// });
    /// </code>
    /// </example>
    /// <param name="endpoints">The route builder.</param>
    /// <param name="pattern">The route pattern (e.g. <c>"/ws/chat"</c>).</param>
    /// <param name="configure">Delegate that configures the endpoint options.</param>
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
    /// <param name="endpoints">The route builder.</param>
    /// <param name="pattern">The route pattern (e.g. <c>"/ws/chat"</c>).</param>
    /// <param name="options">Pre-configured endpoint options.</param>
    public static IEndpointConventionBuilder MapProtobufWebSocket<TSend, TReceive>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        ProtobufWebSocketOptions<TSend, TReceive> options)
        where TSend : class, new()
        where TReceive : class, new()
    {
        // WebSocket connections are long-lived; exempt them from the request-timeout
        // middleware so idle connections are not killed by the server-side timeout.
        return endpoints
            .MapGet(pattern, async (HttpContext ctx) =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                var logger = ctx.RequestServices.GetService<ILoggerFactory>()
                    ?.CreateLogger($"ProtobufWS[{pattern}]");

                var manager = ctx.RequestServices
                    .GetRequiredService<WebSocketConnectionManager<TSend, TReceive>>();

                // Configure keep-alive at the HTTP upgrade level so the OS TCP stack
                // sends pings independently of the application message loop.
                var acceptContext = new WebSocketAcceptContext
                {
                    KeepAliveInterval = options.KeepAliveInterval,
                };

                var ws = await ctx.WebSockets.AcceptWebSocketAsync(acceptContext);
                var connectionId = Guid.NewGuid().ToString("N")[..12];

                await using var connection =
                    new ProtobufWebSocketConnection<TSend, TReceive>(ws, connectionId);
                manager.Add(connection);

                logger?.LogInformation(
                    "Client {ConnectionId} connected to {Pattern} ({Count} total)",
                    connectionId, pattern, manager.Count);

                // Build validation pipelines once per connection, not per message.
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
                    if (options.OnConnect is not null)
                        await options.OnConnect(connection);

#pragma warning disable CS0618 // ReceiveAllAsync: framework call site — suppress until removal
                    await foreach (var message in connection.ReceiveAllAsync(ctx.RequestAborted))
#pragma warning restore CS0618
                    {
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
                catch (OperationCanceledException) { /* normal disconnect */ }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error on connection {ConnectionId}", connectionId);
                    if (options.OnError is not null)
                        await options.OnError(connection, ex);
                }
                finally
                {
                    manager.Remove(connectionId);

                    logger?.LogInformation(
                        "Client {ConnectionId} disconnected from {Pattern} ({Count} remaining)",
                        connectionId, pattern, manager.Count);

                    if (options.OnDisconnect is not null)
                    {
                        try { await options.OnDisconnect(connection); }
                        catch { /* best-effort */ }
                    }
                }
            })
            .WithDisplayName($"ProtobufWebSocket {pattern}")
            .DisableRequestTimeout();
    }
}
