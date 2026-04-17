# Setup & Configuration

ProtobuffEncoder keeps the existing fluent ASP.NET Core setup API and now also supports binding
`ProtobufEncoderOptions` from configuration without changing the transport builder flow.

## Quick Start

```csharp
using ProtobuffEncoder.AspNetCore.Setup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProtobuffEncoder()
    .WithRestFormatters();

var app = builder.Build();
app.Run();
```

## Configuration-Bound Setup

```csharp
using ProtobuffEncoder.AspNetCore.Setup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProtobuffEncoder(builder.Configuration)
    .WithWebSocket(ws => ws.AddEndpoint<NotificationMessage, NotificationMessage>())
    .WithGrpc(grpc => grpc.AddService<WeatherGrpcServiceImpl>());

var app = builder.Build();
app.MapProtobufEndpoints();
app.Run();
```

```json
{
  "ProtobuffEncoder": {
    "EnableMvcFormatters": true,
    "DefaultInvalidMessageBehavior": "Skip"
  }
}
```

Use `AddProtobuffEncoder(builder.Configuration)` for the default `ProtobuffEncoder` section or
`AddProtobuffEncoder(builder.Configuration.GetSection("CustomSection"))` for an explicit section.

## Fluent Setup

```csharp
using ProtobuffEncoder.AspNetCore.Setup;
using ProtobuffEncoder.Transport;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProtobuffEncoder(options =>
{
    options.DefaultInvalidMessageBehavior = InvalidMessageBehavior.Skip;
    options.EnableMvcFormatters = true;
    options.OnGlobalValidationFailure = (_, result) =>
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
app.MapProtobufEndpoints();
app.Run();
```

## Options

`ProtobufEncoderOptions` is available through both direct injection and `IOptions<ProtobufEncoderOptions>`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultInvalidMessageBehavior` | `InvalidMessageBehavior` | `Skip` | Default validation failure behavior across all transports |
| `EnableMvcFormatters` | `bool` | `false` | Auto-register `application/x-protobuf` MVC formatters |
| `OnGlobalValidationFailure` | `Action<object, ValidationResult>?` | `null` | Centralized validation failure callback |

## Transport Builder

The builder returned by `AddProtobuffEncoder(...)` is unchanged in 1.7.0:

| Method | Description |
|--------|-------------|
| `WithRestFormatters()` | Adds REST protobuf MVC formatters |
| `WithWebSocket(Action<WebSocketStrategy>)` | Configures WebSocket endpoint registrations |
| `WithGrpc(Action<GrpcStrategy>)` | Configures gRPC services |
| `AddTransport(IProtobufTransportStrategy)` | Adds a custom transport strategy |
| `MapEndpoints(IEndpointRouteBuilder)` | Maps registered endpoints |

## Standalone Registration

The unified builder is optional.

**REST**
```csharp
builder.Services.AddControllers().AddProtobufFormatters();
```

**WebSocket**
```csharp
builder.Services.AddProtobufWebSocketEndpoint<NotificationMessage, NotificationMessage>();
```

**gRPC**
```csharp
builder.Services.AddGrpc();
builder.Services.AddProtobufGrpcService<WeatherGrpcServiceImpl>();
app.MapGrpcService<WeatherGrpcServiceImpl>();
```
