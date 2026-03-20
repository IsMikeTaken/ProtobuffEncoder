# WebSocket Integration

The `ProtobuffEncoder.WebSockets` package provides a framework for protobuf-encoded WebSocket
communication with connection management, lifecycle hooks, broadcast, validation, and automatic
reconnection.

## Server Setup

### 1. Register connection managers

Each WebSocket endpoint type pair needs a `WebSocketConnectionManager` for connection tracking
and broadcast.

**With the unified setup:**

```C#
builder.Services.AddProtobuffEncoder()
    .WithWebSocket(ws => ws
        .AddEndpoint<NotificationMessage, NotificationMessage>()
        .AddEndpoint<WeatherResponse, WeatherRequest>());
```

**Or standalone:**

```C#
builder.Services.AddProtobufWebSocketEndpoint<NotificationMessage, NotificationMessage>();
builder.Services.AddProtobufWebSocketEndpoint<WeatherResponse, WeatherRequest>();
```

### 2. Map endpoints

```C#
app.UseWebSockets();

app.MapProtobufWebSocket<NotificationMessage, NotificationMessage>("/ws/chat", options =>
{
    options.OnConnect = async conn =>
    {
        await conn.SendAsync(new NotificationMessage
        {
            Source = "Server",
            Text = $"Welcome, {conn.ConnectionId}!"
        });
    };

    options.OnMessage = async (conn, msg) =>
    {
        // Echo back
        await conn.SendAsync(new NotificationMessage
        {
            Source = "EchoBot",
            Text = $"Ack: {msg.Text}"
        });
    };

    options.OnDisconnect = conn =>
    {
        Console.WriteLine($"Client {conn.ConnectionId} left");
        return Task.CompletedTask;
    };
});
```

### Endpoint Options

`ProtobufWebSocketOptions<TSend, TReceive>` provides:

| Property | Type | Description |
|----------|------|-------------|
| `OnConnect` | `Func<connection, Task>` | Called when a client connects |
| `OnMessage` | `Func<connection, TReceive, Task>` | Called for each received message |
| `OnDisconnect` | `Func<connection, Task>` | Called when a client disconnects |
| `OnError` | `Func<connection, Exception, Task>` | Called on connection errors |
| `ConfigureSendValidation` | `Action<ValidationPipeline<TSend>>` | Outgoing validation rules |
| `ConfigureReceiveValidation` | `Action<ValidationPipeline<TReceive>>` | Incoming validation rules |
| `OnInvalidReceive` | `InvalidMessageBehavior` | What to do on validation failure (default: `Skip`) |
| `OnMessageRejected` | `Func<connection, TReceive, ValidationResult, Task>` | Called when a message is rejected |

### Validation

```C#
app.MapProtobufWebSocket<NotificationMessage, NotificationMessage>("/ws/chat", options =>
{
    options.ConfigureReceiveValidation = v =>
        v.Require(m => !string.IsNullOrEmpty(m.Text), "Message text cannot be empty");

    options.OnInvalidReceive = InvalidMessageBehavior.Skip;

    options.OnMessageRejected = async (conn, msg, result) =>
    {
        await conn.SendAsync(new NotificationMessage
        {
            Source = "Server",
            Text = $"Rejected: {result.ErrorMessage}"
        });
    };
});
```

### Broadcasting

Use the `WebSocketConnectionManager<TSend, TReceive>` to broadcast to all connected clients:

```C#
var chatManager = app.Services
    .GetRequiredService<WebSocketConnectionManager<NotificationMessage, NotificationMessage>>();

// Broadcast to all
await chatManager.BroadcastAsync(new NotificationMessage { Text = "Server announcement" });

// Broadcast to all except the sender
await chatManager.BroadcastAsync(message, conn => conn.ConnectionId != senderId);
```

### Connection Properties

Each `ProtobufWebSocketConnection<TSend, TReceive>` exposes:

| Property | Description |
|----------|-------------|
| `ConnectionId` | Unique 12-character identifier |
| `ConnectedAt` | `DateTimeOffset` when the connection was established |
| `IsConnected` | Whether the WebSocket is still open |
| `Stream` | The underlying `ProtobufDuplexStream<TSend, TReceive>` |

## Client Setup

### Basic usage

```C#
using ProtobuffEncoder.WebSockets;

await using var client = new ProtobufWebSocketClient<WeatherRequest, WeatherResponse>(
    new ProtobufWebSocketClientOptions
    {
        ServerUri = new Uri("ws://localhost:5300/ws/weather-stream"),
    });

await client.ConnectAsync();

var response = await client.SendAndReceiveAsync(
    new WeatherRequest { City = "Amsterdam", Days = 3 });
```

### Retry policy

```C#
await using var client = new ProtobufWebSocketClient<NotificationMessage, NotificationMessage>(
    new ProtobufWebSocketClientOptions
    {
        ServerUri = new Uri("ws://localhost:5300/ws/chat"),
        RetryPolicy = new RetryPolicy
        {
            MaxRetries = 5,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(30),
            BackoffMultiplier = 2.0
        },
        OnConnect = () =>
        {
            Console.WriteLine("Connected!");
            return Task.CompletedTask;
        },
        OnRetry = (attempt, delay) =>
        {
            Console.WriteLine($"Retry #{attempt} in {delay.TotalSeconds:F1}s");
            return Task.CompletedTask;
        },
        OnError = ex =>
        {
            Console.WriteLine($"Error: {ex.Message}");
            return Task.CompletedTask;
        }
    });

await client.ConnectAsync();
```

Built-in presets:

| Preset | Behavior |
|--------|----------|
| `RetryPolicy.Default` | 5 retries, 1s initial, 30s max, 2x backoff |
| `RetryPolicy.None` | No retries — fail immediately |

### Client Methods

| Method | Description |
|--------|-------------|
| `ConnectAsync()` | Connect with automatic retry |
| `SendAsync(message)` | Send a single message |
| `ReceiveAsync()` | Receive a single message (null at end of stream) |
| `SendAndReceiveAsync(request)` | Request-response pattern |
| `ReceiveAllAsync()` | Async enumerable of all incoming messages |
| `RunDuplexAsync(outgoing, onReceived)` | Concurrent send and receive |
| `ListenAsync(handler)` | Invoke handler for each incoming message |
| `CloseAsync()` | Graceful disconnect |

### Duplex streaming example

```C#
await client.ConnectAsync();

// Concurrent send and receive
var sendTask = Task.Run(async () =>
{
    for (int i = 0; i < 5; i++)
    {
        await client.SendAsync(new NotificationMessage { Text = $"Message #{i}" });
        await Task.Delay(300);
    }
    await client.CloseAsync();
});

var receiveTask = Task.Run(async () =>
{
    await foreach (var reply in client.ReceiveAllAsync())
        Console.WriteLine($"Received: {reply.Text}");
});

await Task.WhenAll(sendTask, receiveTask);
```

## WebSocketStream

Under the hood, `WebSocketStream` adapts any `System.Net.WebSockets.WebSocket` (server-side
or `ClientWebSocket`) to a `System.IO.Stream`. This allows the existing `ProtobufDuplexStream`
transport layer to work over WebSockets without changes.

```
WebSocket ←→ WebSocketStream ←→ ProtobufDuplexStream<TSend, TReceive>
```

Features:
- Frame reassembly (handles fragmented WebSocket messages)
- Graceful close detection
- Buffer pooling via `MemoryPool<byte>`
- Works identically for server and client WebSockets
