using System.Threading.Tasks;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// A base implementation for a protobuf-enabled WebSocket endpoint.
/// Inherit from this class to handle incoming connections and messages in an object-oriented way.
/// </summary>
/// <typeparam name="TSend">The type of messages sent from the server to the client.</typeparam>
/// <typeparam name="TReceive">The type of messages received from the client.</typeparam>
public abstract class ProtobufWebSocketEndpoint<TSend, TReceive>
    where TSend : class, new()
    where TReceive : class, new()
{
    /// <summary>
    /// Called when a client successfully connects to the endpoint.
    /// </summary>
    public virtual Task OnConnectedAsync(ProtobufWebSocketConnection<TSend, TReceive> connection) 
        => Task.CompletedTask;

    /// <summary>
    /// Called when a message is received from a connected client.
    /// </summary>
    public abstract Task OnMessageReceivedAsync(ProtobufWebSocketConnection<TSend, TReceive> connection, TReceive message);

    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    public virtual Task OnDisconnectedAsync(ProtobufWebSocketConnection<TSend, TReceive> connection) 
        => Task.CompletedTask;

    /// <summary>
    /// Called when an error occurs during the connection lifecycle.
    /// </summary>
    public virtual Task OnErrorAsync(ProtobufWebSocketConnection<TSend, TReceive> connection, Exception exception) 
        => Task.CompletedTask;
}
