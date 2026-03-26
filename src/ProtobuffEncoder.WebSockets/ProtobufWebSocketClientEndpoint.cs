using System.Threading;
using System.Threading.Tasks;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// A base class for a WebSocket client that acts similarly to a server endpoint.
/// Inherit from this class and implement <see cref="OnMessageReceivedAsync"/>
/// to handle incoming messages from the server, while retaining the ability to send messages.
/// </summary>
public abstract class ProtobufWebSocketClientEndpoint<TSend, TReceive> : IAsyncDisposable
    where TSend : class, new()
    where TReceive : class, new()
{
    private readonly ProtobufWebSocketClient<TSend, TReceive> _client;
    private CancellationTokenSource? _listeningCts;
    private Task? _listenTask;

    /// <summary>
    /// Creates a new client endpoint.
    /// </summary>
    protected ProtobufWebSocketClientEndpoint(ProtobufWebSocketClientOptions options)
    {
        // Intercept connect/disconnect callbacks to route to virtual methods.
        var originalConnect = options.OnConnect;
        options.OnConnect = async () =>
        {
            if (originalConnect is not null) await originalConnect();
            await OnConnectedAsync();
        };

        var originalDisconnect = options.OnDisconnect;
        options.OnDisconnect = async () =>
        {
            if (originalDisconnect is not null) await originalDisconnect();
            await OnDisconnectedAsync();
        };

        var originalError = options.OnError;
        options.OnError = async ex =>
        {
            if (originalError is not null) await originalError(ex);
            await OnErrorAsync(ex);
        };

        _client = new ProtobufWebSocketClient<TSend, TReceive>(options);
    }

    /// <summary>
    /// The underlying managed WebSocket client.
    /// </summary>
    public ProtobufWebSocketClient<TSend, TReceive> Client => _client;

    /// <summary>
    /// Connects to the server and begins listening for messages in the background.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _client.ConnectAsync(cancellationToken);

        _listeningCts = new CancellationTokenSource();
        _listenTask = _client.ListenAsync(msg => OnMessageReceivedAsync(msg), _listeningCts.Token);
    }

    /// <summary>
    /// Gracefully closes the connection and stops listening.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_listeningCts is not null)
        {
            _listeningCts.Cancel();
        }

        await _client.CloseAsync(cancellationToken);

        if (_listenTask is not null)
        {
            try { await _listenTask; }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// Sends a message to the server.
    /// </summary>
    public Task SendAsync(TSend message, CancellationToken cancellationToken = default)
        => _client.SendAsync(message, cancellationToken);

    /// <summary>
    /// Called when the client successfully connects to the server.
    /// </summary>
    protected virtual Task OnConnectedAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when a message is received from the server.
    /// </summary>
    protected abstract Task OnMessageReceivedAsync(TReceive message);

    /// <summary>
    /// Called when the connection to the server is closed.
    /// </summary>
    protected virtual Task OnDisconnectedAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when an error occurs during connection or receiving.
    /// </summary>
    protected virtual Task OnErrorAsync(Exception exception) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_listeningCts is not null)
        {
            _listeningCts.Cancel();
            _listeningCts.Dispose();
        }
        await _client.DisposeAsync();
    }
}
