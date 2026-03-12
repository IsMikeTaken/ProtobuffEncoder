using ProtobuffEncoder.Transport;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// Configures behavior for a protobuf WebSocket endpoint registered via
/// <see cref="WebSocketEndpointRouteBuilderExtensions.MapProtobufWebSocket{TSend, TReceive}"/>.
/// </summary>
public sealed class ProtobufWebSocketOptions<TSend, TReceive>
    where TSend : class, new()
    where TReceive : class, new()
{
    /// <summary>
    /// Called when a new client connects. Use this to send a welcome message,
    /// log the connection, or initialize per-connection state.
    /// </summary>
    public Func<ProtobufWebSocketConnection<TSend, TReceive>, Task>? OnConnect { get; set; }

    /// <summary>
    /// Called when a client disconnects (graceful close or error).
    /// </summary>
    public Func<ProtobufWebSocketConnection<TSend, TReceive>, Task>? OnDisconnect { get; set; }

    /// <summary>
    /// Called when an error occurs on a connection. If not set, errors are silently caught.
    /// </summary>
    public Func<ProtobufWebSocketConnection<TSend, TReceive>, Exception, Task>? OnError { get; set; }

    /// <summary>
    /// The message handler invoked for each received message.
    /// Receives the connection (for replying or broadcasting) and the deserialized message.
    /// </summary>
    public Func<ProtobufWebSocketConnection<TSend, TReceive>, TReceive, Task>? OnMessage { get; set; }

    /// <summary>
    /// Configures the validation pipeline for outgoing (server-to-client) messages.
    /// </summary>
    public Action<ValidationPipeline<TSend>>? ConfigureSendValidation { get; set; }

    /// <summary>
    /// Configures the validation pipeline for incoming (client-to-server) messages.
    /// </summary>
    public Action<ValidationPipeline<TReceive>>? ConfigureReceiveValidation { get; set; }

    /// <summary>
    /// Behavior when an incoming message fails validation. Defaults to <see cref="InvalidMessageBehavior.Skip"/>.
    /// Set to Skip for long-lived WebSocket connections so a single bad message doesn't kill the connection.
    /// </summary>
    public InvalidMessageBehavior OnInvalidReceive { get; set; } = InvalidMessageBehavior.Skip;

    /// <summary>
    /// Called when an incoming message fails receive validation (when behavior is Skip or ReturnNull).
    /// </summary>
    public Func<ProtobufWebSocketConnection<TSend, TReceive>, TReceive, ValidationResult, Task>? OnMessageRejected { get; set; }
}
