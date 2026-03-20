# WebSocket Integration

The `ProtobuffEncoder.WebSockets` package provides a managed WebSocket client and server with protobuf duplex streaming, automatic reconnection, connection management, and lifecycle hooks.

## Server-Side

### Endpoint Registration

```csharp
builder.Services.AddProtobuffEncoder()
    .WithWebSocket(ws => ws
        .AddEndpoint<NotificationMessage, NotificationMessage>());
```

### Endpoint Configuration

```csharp
.WithWebSocket(ws => ws
    .AddEndpoint<WeatherResponse, WeatherRequest>(options =>
    {
        options.OnConnect = async conn =>
        {
            Console.WriteLine($"Client {conn.ConnectionId} connected");
            await conn.SendAsync(new WeatherResponse { Message = "Welcome!" });
        };

        options.OnDisconnect = async conn =>
        {
            Console.WriteLine($"Client {conn.ConnectionId} disconnected");
        };

        options.OnMessage = async (conn, request) =>
        {
            var response = ProcessWeatherRequest(request);
            await conn.SendAsync(response);
        };

        options.OnError = async (conn, ex) =>
        {
            logger.LogError(ex, "WebSocket error on {Id}", conn.ConnectionId);
        };

        // Validation
        options.ConfigureSendValidation = v =>
            v.Require(m => !string.IsNullOrEmpty(m.City), "City required");

        options.ConfigureReceiveValidation = v =>
            v.Require(m => m.Days > 0, "Days must be positive");

        options.OnInvalidReceive = InvalidMessageBehavior.Skip;

        options.OnMessageRejected = async (conn, msg, result) =>
        {
            logger.LogWarning("Rejected from {Id}: {Error}",
                conn.ConnectionId, result.ErrorMessage);
        };
    }))
```

### ProtobufWebSocketOptions

| Property | Type | Description |
|----------|------|-------------|
| `OnConnect` | `Func<Connection, Task>?` | Called when a client connects |
| `OnDisconnect` | `Func<Connection, Task>?` | Called when a client disconnects |
| `OnError` | `Func<Connection, Exception, Task>?` | Called on connection error |
| `OnMessage` | `Func<Connection, TReceive, Task>?` | Called for each received message |
| `ConfigureSendValidation` | `Action<ValidationPipeline<TSend>>?` | Outgoing validation rules |
| `ConfigureReceiveValidation` | `Action<ValidationPipeline<TReceive>>?` | Incoming validation rules |
| `OnInvalidReceive` | `InvalidMessageBehavior` | Behavior for invalid messages (default: `Skip`) |
| `OnMessageRejected` | `Func<Connection, TReceive, ValidationResult, Task>?` | Rejection callback |

### ProtobufWebSocketConnection

Each connected client is represented by a `ProtobufWebSocketConnection<TSend, TReceive>`:

| Property / Method | Description |
|-------------------|-------------|
| `ConnectionId` | Unique identifier |
| `ConnectedAt` | Connection timestamp |
| `IsConnected` | Whether the WebSocket is still open |
| `Stream` | The underlying `ProtobufDuplexStream` for advanced patterns |
| `SendAsync(TSend, CancellationToken)` | Send a message |
| `ReceiveAsync(CancellationToken)` | Receive a message |
| `ReceiveAllAsync(CancellationToken)` | Async enumerable of incoming messages |

### WebSocketConnectionManager

Thread-safe tracker of all active connections for an endpoint. Supports broadcast and filtered broadcast.

```csharp
// Inject via DI
var manager = app.Services.GetRequiredService<
    WebSocketConnectionManager<NotificationMessage, NotificationMessage>>();

// Broadcast to all
await manager.BroadcastAsync(new NotificationMessage { Text = "Hello everyone!" });

// Broadcast with filter
await manager.BroadcastAsync(
    new NotificationMessage { Text = "VIP only" },
    conn => conn.ConnectionId.StartsWith("vip-"));

// Connection count
Console.WriteLine($"Active: {manager.Count}");

// Get specific connection
var conn = manager.GetConnection("connection-id");

// Snapshot of all connections
IReadOnlyCollection<Connection> all = manager.Connections;
```

## Client-Side

### ProtobufWebSocketClient

```csharp
using ProtobuffEncoder.WebSockets;

await using var client = new ProtobufWebSocketClient<WeatherRequest, WeatherResponse>(
    new ProtobufWebSocketClientOptions
    {
        ServerUri = new Uri("ws://localhost:5300/ws/weather"),
        RetryPolicy = RetryPolicy.Default,
        OnConnect = () => { Console.WriteLine("Connected!"); return Task.CompletedTask; },
        OnDisconnect = () => { Console.WriteLine("Disconnected"); return Task.CompletedTask; },
        OnError = ex => { Console.WriteLine($"Error: {ex.Message}"); return Task.CompletedTask; },
        OnRetry = (attempt, delay) =>
        {
            Console.WriteLine($"Retry #{attempt} in {delay}");
            return Task.CompletedTask;
        }
    });

await client.ConnectAsync();
```

### Client API

| Property / Method | Description |
|-------------------|-------------|
| `IsConnected` | Whether the client is connected |
| `Stream` | Underlying `ProtobufDuplexStream` (available after connect) |
| `ConnectAsync(CancellationToken)` | Connect with automatic retry |
| `SendAsync(TSend, CancellationToken)` | Send a message |
| `ReceiveAsync(CancellationToken)` | Receive a single message |
| `SendAndReceiveAsync(TSend, CancellationToken)` | Request-response pattern |
| `ReceiveAllAsync(CancellationToken)` | Async stream of incoming messages |
| `RunDuplexAsync(IAsyncEnumerable, Func, CancellationToken)` | Concurrent send + receive |
| `ListenAsync(Func, CancellationToken)` | Listen with handler |
| `CloseAsync(CancellationToken)` | Graceful close |

### Client Patterns

#### Request-Response

```csharp
var response = await client.SendAndReceiveAsync(
    new WeatherRequest { City = "Amsterdam" });
Console.WriteLine($"Temperature: {response.Temperature}");
```

#### Continuous Listening

```csharp
await client.ListenAsync(async response =>
{
    Console.WriteLine($"Update: {response.Temperature}C");
}, ct);
```

#### Bidirectional Streaming

```csharp
await client.RunDuplexAsync(
    outgoing: GenerateRequestsAsync(),
    onReceived: async response =>
    {
        Console.WriteLine($"Got: {response.Data}");
    },
    ct);
```

## RetryPolicy

Configures exponential backoff retry for the WebSocket client:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxRetries` | `int` | `5` | Maximum retry attempts. `0` = no retry |
| `InitialDelay` | `TimeSpan` | `1 second` | Delay before first retry |
| `MaxDelay` | `TimeSpan` | `30 seconds` | Maximum delay cap |
| `BackoffMultiplier` | `double` | `2.0` | Multiplier after each attempt |

### Presets

```csharp
// Default: 5 retries, 1s initial, 30s max, 2x backoff
RetryPolicy.Default

// No retries - fail immediately
RetryPolicy.None
```

### Custom Policy

```csharp
var policy = new RetryPolicy
{
    MaxRetries = 10,
    InitialDelay = TimeSpan.FromMilliseconds(500),
    MaxDelay = TimeSpan.FromMinutes(1),
    BackoffMultiplier = 1.5
};
```

### Retry Delay Formula

```
delay = min(InitialDelay * BackoffMultiplier^attempt, MaxDelay)
```

Example with defaults: 1s, 2s, 4s, 8s, 16s (capped at 30s).

## ProtobufWebSocketClientOptions

| Property | Type | Description |
|----------|------|-------------|
| `ServerUri` | `Uri` (required) | WebSocket server URI |
| `RetryPolicy` | `RetryPolicy` | Retry configuration |
| `OnConnect` | `Func<Task>?` | Called after successful connect/reconnect |
| `OnDisconnect` | `Func<Task>?` | Called on disconnect |
| `OnError` | `Func<Exception, Task>?` | Called on errors |
| `OnRetry` | `Func<int, TimeSpan, Task>?` | Called before each retry (attempt number, delay) |
| `ConfigureWebSocket` | `Action<ClientWebSocket>?` | Configure underlying WebSocket (headers, proxy, etc.) |

### Configuring the WebSocket

```csharp
ConfigureWebSocket = ws =>
{
    ws.Options.SetRequestHeader("Authorization", "Bearer token");
    ws.Options.AddSubProtocol("protobuf");
    ws.Options.Proxy = new WebProxy("http://proxy:8080");
}
```

## WebSocketStream

Internal `Stream` adapter that bridges `System.Net.WebSockets.WebSocket` to the `Stream` API used by `ProtobufDuplexStream`. Handles binary message framing and proper WebSocket close handshake.
