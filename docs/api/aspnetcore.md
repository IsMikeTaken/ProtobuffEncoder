# ASP.NET Core Integration

The `ProtobuffEncoder.AspNetCore` package provides REST API formatters, HttpClient extensions,
and a fluent builder for configuring REST, WebSocket, and gRPC transports from one entry point.

## Setup with Options Callback

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProtobuffEncoder(options =>
{
    options.EnableMvcFormatters = true;
    options.DefaultInvalidMessageBehavior = InvalidMessageBehavior.Skip;
})
.WithRestFormatters()
.WithWebSocket(ws => ws.AddEndpoint<NotificationMessage, NotificationMessage>())
.WithGrpc(grpc => grpc.AddService<WeatherGrpcServiceImpl>());

var app = builder.Build();
app.MapProtobufEndpoints();
```

## Setup from Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProtobuffEncoder(builder.Configuration)
    .WithGrpc(grpc => grpc.AddService<WeatherGrpcServiceImpl>());
```

The configuration overload binds the default `ProtobuffEncoder` section. Use
`AddProtobuffEncoder(builder.Configuration.GetSection("MySection"))` to bind an explicit section.

## ProtobufEncoderOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableMvcFormatters` | `bool` | `false` | Enable `application/x-protobuf` MVC formatters |
| `DefaultInvalidMessageBehavior` | `InvalidMessageBehavior` | `Skip` | Default validation failure behavior |
| `OnGlobalValidationFailure` | `Action<object, ValidationResult>?` | `null` | Centralized validation failure callback |

## ProtobufEncoderBuilder

The fluent builder returned by `AddProtobuffEncoder()` is preserved in 1.7.0.

| Method | Description |
|--------|-------------|
| `WithRestFormatters()` | Add MVC input/output formatters for `application/x-protobuf` |
| `WithWebSocket(Action<WebSocketStrategy>)` | Configure WebSocket endpoints |
| `WithGrpc(Action<GrpcStrategy>)` | Configure gRPC services |
| `AddTransport(IProtobufTransportStrategy)` | Register a custom transport strategy |
| `MapEndpoints(IEndpointRouteBuilder)` | Map all registered endpoints in the pipeline |
| `Strategies` | Inspect registered strategies |

## REST Formatters

`WithRestFormatters()` and `AddControllers().AddProtobufFormatters()` both register the same
`ProtobufInputFormatter` and `ProtobufOutputFormatter` pair for `application/x-protobuf`.

## HttpClient Extensions

```csharp
var response = await httpClient.PostProtobufAsync<WeatherRequest, WeatherResponse>(
    "api/weather/forecast",
    new WeatherRequest { City = "Amsterdam", Days = 5 });
```

## Complete Server Example

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProtobuffEncoder(builder.Configuration)
    .WithRestFormatters();

var app = builder.Build();
app.MapControllers();
app.Run();
```
