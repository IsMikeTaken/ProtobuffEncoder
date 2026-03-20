# Setup & Configuration

ProtobuffEncoder provides a unified setup API based on the **Options pattern** and **Strategy pattern**
to configure all transports (REST, WebSocket, gRPC) from a single entry point.

## Quick Start

```csharp
// Program.cs
using ProtobuffEncoder.AspNetCore.Setup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProtobuffEncoder()
    .WithRestFormatters();

var app = builder.Build();
app.Run();
```

That's the minimal setup — a single line that registers protobuf MVC formatters for REST APIs.

---

## Full Setup (All Transports)

```csharp
using ProtobuffEncoder.AspNetCore.Setup;
using ProtobuffEncoder.Transport;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProtobuffEncoder(options =>
{
    options.DefaultInvalidMessageBehavior = InvalidMessageBehavior.Skip;
    options.EnableMvcFormatters = true;
    options.OnGlobalValidationFailure = (message, result) =>
        Console.WriteLine($"Validation failed: {result.ErrorMessage}");
})
.WithRestFormatters()
.WithWebSocket(ws => ws
    .AddEndpoint<NotificationMessage, NotificationMessage>()
    .AddEndpoint<WeatherResponse, WeatherRequest>())
.WithGrpc(grpc => grpc
    .UseKestrel(httpPort: 5400, grpcPort: 5401)
    .AddService<WeatherGrpcServiceImpl>()
    .AddService<ChatGrpcServiceImpl>());

var app = builder.Build();

app.UseWebSockets();
app.UseDefaultFiles();
app.UseStaticFiles();

// Maps all auto-mapped endpoints (gRPC services registered with autoMap: true)
app.MapProtobufEndpoints();

// WebSocket endpoints are still mapped explicitly for full control over routes/options
app.MapProtobufWebSocket<NotificationMessage, NotificationMessage>("/ws/chat", options =>
{
    options.OnMessage = async (conn, msg) => await conn.SendAsync(reply);
});

app.Run();
```

---

## Architecture

### Options Pattern — `ProtobufEncoderOptions`

Central configuration injected via `IOptions<ProtobufEncoderOptions>`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultInvalidMessageBehavior` | `InvalidMessageBehavior` | `Skip` | Default validation failure behavior across all transports |
| `EnableMvcFormatters` | `bool` | `false` | Auto-register `application/x-protobuf` MVC formatters |
| `OnGlobalValidationFailure` | `Action<object, ValidationResult>?` | `null` | Centralized validation failure callback for logging/telemetry |

### Strategy Pattern — `IProtobufTransportStrategy`

Each transport is a strategy that encapsulates its own DI registration and endpoint mapping.

```
IProtobufTransportStrategy
├── ConfigureServices(IServiceCollection, ProtobufEncoderOptions)
└── ConfigureEndpoints(IEndpointRouteBuilder, ProtobufEncoderOptions)
```

**Built-in strategies:**

| Strategy | Builder Method | Registers |
|----------|---------------|-----------|
| `RestFormatterStrategy` | `.WithRestFormatters()` | MVC input/output formatters for `application/x-protobuf` |
| `WebSocketStrategy` | `.WithWebSocket(ws => ...)` | `WebSocketConnectionManager<TSend,TReceive>` singletons |
| `GrpcStrategy` | `.WithGrpc(grpc => ...)` | gRPC infrastructure + `IServiceMethodProvider<T>` per service |

### Builder — `ProtobufEncoderBuilder`

The fluent entry point returned by `AddProtobuffEncoder()`.

```csharp
builder.Services.AddProtobuffEncoder(options => { ... })
    .WithRestFormatters()              // REST strategy
    .WithWebSocket(ws => { ... })      // WebSocket strategy
    .WithGrpc(grpc => { ... })         // gRPC strategy
    .AddTransport(new MyStrategy());   // Custom strategy
```

---

## Transport-Specific Details

### REST Formatters

```csharp
builder.Services.AddProtobuffEncoder()
    .WithRestFormatters();
```

Registers `ProtobufInputFormatter` and `ProtobufOutputFormatter` so ASP.NET Core controllers
automatically serialize/deserialize `application/x-protobuf` request and response bodies.

Alternatively, set `options.EnableMvcFormatters = true` in the options callback to auto-register
without calling `.WithRestFormatters()` explicitly.

### WebSocket

```csharp
builder.Services.AddProtobuffEncoder()
    .WithWebSocket(ws => ws
        .AddEndpoint<NotificationMessage, NotificationMessage>()
        .AddEndpoint<WeatherResponse, WeatherRequest>());
```

Each `.AddEndpoint<TSend, TReceive>()` registers a `WebSocketConnectionManager<TSend, TReceive>`
singleton for connection tracking and broadcast support. Endpoints are still mapped explicitly
via `app.MapProtobufWebSocket(...)` for full control over routes and lifecycle hooks.

### gRPC

```csharp
builder.Services.AddProtobuffEncoder()
    .WithGrpc(grpc => grpc
        .UseKestrel(httpPort: 5400, grpcPort: 5401)
        .AddService<WeatherGrpcServiceImpl>()
        .AddService<ChatGrpcServiceImpl>());

// In the app pipeline:
app.MapProtobufEndpoints(); // auto-maps all gRPC services
```

#### `UseKestrel(httpPort, grpcPort)`

Configures two Kestrel endpoints because HTTP/2 cannot be negotiated over cleartext (no ALPN).
The `httpPort` listens with HTTP/1.1 for browser dashboards and REST APIs. The `grpcPort`
listens with HTTP/2 for gRPC calls.

| Port | Protocol | Purpose |
|------|----------|---------|
| `httpPort` | HTTP/1.1 | Browser dashboard, REST, health checks |
| `grpcPort` | HTTP/2 | gRPC calls |

When using HTTPS, both protocols are negotiated via ALPN on a single port and `UseKestrel()`
is not needed.

#### `AddService<T>(autoMap)`

Each `.AddService<T>()` call:
1. Registers `AddGrpc()` (once)
2. Registers the service as scoped
3. Registers `IServiceMethodProvider<T>` for attribute-based method discovery
4. Auto-maps the gRPC endpoint (set `autoMap: false` to map manually)

Pass `autoMap: false` when you need per-service endpoint configuration:

```csharp
.WithGrpc(grpc => grpc
    .UseKestrel(httpPort: 5400, grpcPort: 5401)
    .AddService<WeatherGrpcServiceImpl>(autoMap: false));

// Later, map manually:
app.MapGrpcService<WeatherGrpcServiceImpl>();
```

---

## Custom Transport Strategy

Implement `IProtobufTransportStrategy` to add your own transport:

```csharp
public class MqttStrategy : IProtobufTransportStrategy
{
    private readonly string _brokerUrl;

    public MqttStrategy(string brokerUrl) => _brokerUrl = brokerUrl;

    public void ConfigureServices(IServiceCollection services, ProtobufEncoderOptions options)
    {
        services.AddSingleton(new MqttProtobufClient(_brokerUrl));
    }

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints, ProtobufEncoderOptions options)
    {
        // Map MQTT-specific endpoints if needed
    }
}

// Registration:
builder.Services.AddProtobuffEncoder()
    .AddTransport(new MqttStrategy("mqtt://broker:1883"));
```

---

## Without the Unified Setup

Each transport can still be registered independently using its own extension methods.
The unified builder is optional — it provides convenience and consistency, not a hard dependency.

**REST (standalone):**
```csharp
builder.Services.AddControllers().AddProtobufFormatters();
```

**WebSocket (standalone):**
```csharp
builder.Services.AddProtobufWebSocketEndpoint<NotificationMessage, NotificationMessage>();
```

**gRPC (standalone):**
```csharp
// Configure Kestrel — HTTP/1.1 for browser, HTTP/2 for gRPC
builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenLocalhost(5400, o => o.Protocols = HttpProtocols.Http1);
    k.ListenLocalhost(5401, o => o.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();
builder.Services.AddProtobufGrpcService<WeatherGrpcServiceImpl>();
app.MapGrpcService<WeatherGrpcServiceImpl>();
```

---

## Accessing Options at Runtime

Inject `ProtobufEncoderOptions` or `IOptions<ProtobufEncoderOptions>` anywhere:

```csharp
public class MyService
{
    private readonly ProtobufEncoderOptions _options;

    public MyService(ProtobufEncoderOptions options)
    {
        _options = options;
    }

    public void ProcessMessage<T>(T message)
    {
        // Use global validation behavior
        if (_options.DefaultInvalidMessageBehavior == InvalidMessageBehavior.Throw)
            throw new InvalidOperationException("...");
    }
}
```

---

## Project References

| Your project needs... | Add reference to |
|----------------------|-----------------|
| REST formatters only | `ProtobuffEncoder.AspNetCore` |
| WebSocket transport | `ProtobuffEncoder.WebSockets` (pulled in by AspNetCore) |
| gRPC transport | `ProtobuffEncoder.Grpc` (pulled in by AspNetCore) |
| Core encoding only | `ProtobuffEncoder` |
| Shared contracts | `ProtobuffEncoder.Contracts` |

When you reference `ProtobuffEncoder.AspNetCore`, all transport libraries are transitively
available — the unified setup builder can configure any combination.
