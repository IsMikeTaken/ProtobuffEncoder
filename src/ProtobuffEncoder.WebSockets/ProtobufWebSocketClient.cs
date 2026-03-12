using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using ProtobuffEncoder.Transport;

namespace ProtobuffEncoder.WebSockets;

/// <summary>
/// A managed protobuf WebSocket client with automatic reconnection, lifecycle hooks,
/// and access to the underlying <see cref="ProtobufDuplexStream{TSend, TReceive}"/>.
/// <para>
/// Supports all duplex patterns: fire-and-forget send, request-response,
/// async enumerable receive, and concurrent bidirectional streaming.
/// </para>
/// </summary>
/// <example>
/// <code>
/// await using var client = new ProtobufWebSocketClient&lt;WeatherRequest, WeatherResponse&gt;(
///     new ProtobufWebSocketClientOptions
///     {
///         ServerUri = new Uri("ws://localhost:5300/ws/weather"),
///         RetryPolicy = RetryPolicy.Default,
///         OnConnect = () => { Console.WriteLine("Connected!"); return Task.CompletedTask; },
///         OnRetry = (attempt, delay) => { Console.WriteLine($"Retry #{attempt} in {delay}"); return Task.CompletedTask; }
///     });
///
/// await client.ConnectAsync();
/// var response = await client.SendAndReceiveAsync(new WeatherRequest { City = "Amsterdam", Days = 3 });
/// </code>
/// </example>
public sealed class ProtobufWebSocketClient<TSend, TReceive> : IAsyncDisposable
    where TSend : class, new()
    where TReceive : class, new()
{
    private readonly ProtobufWebSocketClientOptions _options;
    private ClientWebSocket? _ws;
    private WebSocketStream? _wsStream;
    private ProtobufDuplexStream<TSend, TReceive>? _duplex;

    public ProtobufWebSocketClient(ProtobufWebSocketClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>Whether the client is currently connected.</summary>
    public bool IsConnected => _ws?.State == WebSocketState.Open;

    /// <summary>
    /// The underlying duplex stream. Available after <see cref="ConnectAsync"/> succeeds.
    /// Use for advanced patterns like <c>RunDuplexAsync</c> or <c>ProcessAsync</c>.
    /// </summary>
    public ProtobufDuplexStream<TSend, TReceive>? Stream => _duplex;

    /// <summary>
    /// Connects to the server with automatic retry per the configured <see cref="RetryPolicy"/>.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await ConnectWithRetryAsync(cancellationToken);
    }

    /// <summary>Sends a single message. Thread-safe.</summary>
    public async Task SendAsync(TSend message, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _duplex!.SendAsync(message, cancellationToken);
    }

    /// <summary>Receives a single message. Returns null at end of stream.</summary>
    public async Task<TReceive?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        return await _duplex!.ReceiveAsync(cancellationToken);
    }

    /// <summary>Sends a request and waits for a single response (request-response pattern).</summary>
    public async Task<TReceive?> SendAndReceiveAsync(TSend request, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        return await _duplex!.SendAndReceiveAsync(request, cancellationToken);
    }

    /// <summary>Receives all messages as an async stream until disconnect.</summary>
    public async IAsyncEnumerable<TReceive> ReceiveAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await foreach (var message in _duplex!.ReceiveAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    /// <summary>
    /// Runs concurrent send and receive. Sends messages from <paramref name="outgoing"/>
    /// while invoking <paramref name="onReceived"/> for each incoming message.
    /// </summary>
    public async Task RunDuplexAsync(
        IAsyncEnumerable<TSend> outgoing,
        Func<TReceive, Task> onReceived,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _duplex!.RunDuplexAsync(outgoing, onReceived, cancellationToken);
    }

    /// <summary>Listens for incoming messages, invoking the handler for each one.</summary>
    public async Task ListenAsync(Func<TReceive, Task> handler, CancellationToken cancellationToken = default)
    {
        EnsureConnected();
        await _duplex!.ListenAsync(handler, cancellationToken);
    }

    /// <summary>Gracefully closes the WebSocket connection.</summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_ws is { State: WebSocketState.Open })
        {
            await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", cancellationToken);
        }

        if (_options.OnDisconnect is not null)
            await _options.OnDisconnect();
    }

    public async ValueTask DisposeAsync()
    {
        if (_duplex is not null)
            await _duplex.DisposeAsync();

        _ws?.Dispose();
    }

    #region Internal

    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        var policy = _options.RetryPolicy;
        int attempt = 0;

        while (true)
        {
            try
            {
                // Dispose previous connection state if reconnecting
                if (_duplex is not null) await _duplex.DisposeAsync();
                _ws?.Dispose();

                _ws = new ClientWebSocket();
                _options.ConfigureWebSocket?.Invoke(_ws);

                await _ws.ConnectAsync(_options.ServerUri, cancellationToken);

                _wsStream = new WebSocketStream(_ws);
                _duplex = new ProtobufDuplexStream<TSend, TReceive>(_wsStream, ownsStream: true);

                if (_options.OnConnect is not null)
                    await _options.OnConnect();

                return; // Success
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (_options.OnError is not null)
                    await _options.OnError(ex);

                attempt++;
                if (policy.MaxRetries <= 0 || attempt > policy.MaxRetries)
                    throw;

                var delay = policy.GetDelay(attempt - 1);

                if (_options.OnRetry is not null)
                    await _options.OnRetry(attempt, delay);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private void EnsureConnected()
    {
        if (_duplex is null || !IsConnected)
            throw new InvalidOperationException(
                "Not connected. Call ConnectAsync() first.");
    }

    #endregion
}
