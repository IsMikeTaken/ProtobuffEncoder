# ASP.NET Core Integration

The `ProtobuffEncoder.AspNetCore` package provides REST API formatters, HttpClient extensions, and a fluent builder for configuring all transport strategies (REST, WebSocket, gRPC).

## Setup with Builder Pattern

```C#
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProtobuffEncoder(options =>
{
    options.EnableMvcFormatters = true;
    options.DefaultInvalidMessageBehavior = InvalidMessageBehavior.Skip;
})
.WithRestFormatters()
.WithWebSocket(ws => ws
    .AddEndpoint<NotificationMessage, NotificationMessage>())
.WithGrpc(grpc => grpc
    .AddService<WeatherGrpcServiceImpl>());

var app = builder.Build();
```

## ProtobufEncoderOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableMvcFormatters` | `bool` | `false` | Enable `application/x-protobuf` MVC formatters |
| `DefaultInvalidMessageBehavior` | `InvalidMessageBehavior` | `Skip` | Default validation failure behavior |
| `OnGlobalValidationFailure` | `Action<object, ValidationResult>?` | `null` | Centralized validation failure callback |

## ProtobufEncoderBuilder

Fluent builder returned by `AddProtobuffEncoder()`:

| Method | Description |
|--------|-------------|
| `WithRestFormatters()` | Add MVC input/output formatters for `application/x-protobuf` |
| `WithWebSocket(Action<WebSocketStrategy>)` | Configure WebSocket endpoints |
| `WithGrpc(Action<GrpcStrategy>)` | Configure gRPC services |
| `AddTransport(IProtobufTransportStrategy)` | Register a custom transport strategy |
| `MapEndpoints(IEndpointRouteBuilder)` | Map all registered endpoints in the pipeline |
| `Strategies` | Inspect registered strategies |

## Transport Strategies

The builder uses the strategy pattern. Each transport implements `IProtobufTransportStrategy`:

```C#
public interface IProtobufTransportStrategy
{
    void ConfigureServices(IServiceCollection services, ProtobufEncoderOptions options);
    void ConfigureEndpoints(IEndpointRouteBuilder endpoints, ProtobufEncoderOptions options);
}
```

Built-in strategies:

| Strategy | Description |
|----------|-------------|
| `RestFormatterStrategy` | Adds `ProtobufInputFormatter` and `ProtobufOutputFormatter` to MVC |
| `WebSocketStrategy` | Registers connection managers and WebSocket endpoints |
| `GrpcStrategy` | Registers gRPC services with protobuf marshalling |

## REST Formatters

### ProtobufInputFormatter

Reads `application/x-protobuf` request bodies and deserializes them:

```C#
// Automatically used by MVC when client sends Content-Type: application/x-protobuf
[HttpPost("/api/orders")]
public IActionResult CreateOrder([FromBody] OrderMessage order)
{
    // order is deserialized from protobuf binary
    return Ok();
}
```

Requirements:
- The model type must have a parameterless constructor
- Request body is read fully into memory, then decoded

### ProtobufOutputFormatter

Writes `application/x-protobuf` response bodies:

```C#
[HttpGet("/api/orders/{id}")]
public OrderMessage GetOrder(int id)
{
    // When client sends Accept: application/x-protobuf,
    // the response is serialized as protobuf binary
    return new OrderMessage { OrderId = id, Total = 99.99 };
}
```

The formatter sets `Content-Length` automatically.

### Media Type

```C#
public static class ProtobufMediaType
{
    public const string Protobuf = "application/x-protobuf";
}
```

## ProtobufHttpContent

`HttpContent` implementation for sending protobuf bodies with `HttpClient`:

```C#
var content = new ProtobufHttpContent(orderMessage);
// Content-Type is automatically set to application/x-protobuf
// Content-Length is computed from the serialized bytes
```

## HttpClient Extensions

Convenience methods for sending and receiving protobuf messages:

### PostProtobufAsync (with response)

```C#
var response = await httpClient.PostProtobufAsync<OrderRequest, OrderResponse>(
    "/api/orders",
    new OrderRequest { OrderId = 1 });
// response is a deserialized OrderResponse
```

### PostProtobufAsync (fire-and-forget)

```C#
var httpResponse = await httpClient.PostProtobufAsync(
    "/api/notifications",
    new NotificationMessage { Text = "Hello" });
// Returns HttpResponseMessage, no deserialization
```

### GetProtobufAsync

```C#
var status = await httpClient.GetProtobufAsync<StatusResponse>("/api/status");
// Sends Accept: application/x-protobuf, deserializes response
```

## Complete Server Example

```C#
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProtobuffEncoder(options =>
{
    options.EnableMvcFormatters = true;
})
.WithRestFormatters();

var app = builder.Build();
app.MapControllers();
app.Run();
```

```C#
[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
{
    [HttpPost("forecast")]
    public WeatherResponse GetForecast([FromBody] WeatherRequest request)
    {
        return new WeatherResponse
        {
            City = request.City,
            Temperature = 22.5,
            Description = "Sunny"
        };
    }
}
```

## Complete Client Example

```C#
var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

var response = await client.PostProtobufAsync<WeatherRequest, WeatherResponse>(
    "api/weather/forecast",
    new WeatherRequest { City = "Amsterdam", Days = 5 });

Console.WriteLine($"{response.City}: {response.Temperature}C, {response.Description}");
```

