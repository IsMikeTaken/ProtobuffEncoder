using System.Net.WebSockets;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// Configuration for <see cref="ProtobufWebSocketClient{TSend, TReceive}"/>.
/// </summary>
public sealed class ProtobufWebSocketClientOptions
{
    /// <summary>The WebSocket server URI to connect to (e.g., <c>ws://localhost:5300/ws/chat</c>).</summary>
    public required Uri ServerUri { get; set; }

    /// <summary>Retry policy for automatic reconnection. Defaults to <see cref="RetryPolicy.Default"/>.</summary>
    public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Default;

    /// <summary>Called after a successful connection or reconnection.</summary>
    public Func<Task>? OnConnect { get; set; }

    /// <summary>Called when the connection is lost (graceful or error).</summary>
    public Func<Task>? OnDisconnect { get; set; }

    /// <summary>Called when an error occurs during send/receive.</summary>
    public Func<Exception, Task>? OnError { get; set; }

    /// <summary>Called before each retry attempt. Receives the attempt number (1-based) and the delay.</summary>
    public Func<int, TimeSpan, Task>? OnRetry { get; set; }

    /// <summary>
    /// Optional configurator for the underlying <see cref="ClientWebSocket"/>
    /// (e.g., to set headers, subprotocols, or proxy settings).
    /// </summary>
    public Action<ClientWebSocket>? ConfigureWebSocket { get; set; }
}
