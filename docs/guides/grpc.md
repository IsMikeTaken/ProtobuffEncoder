# gRPC Integration

ProtobuffEncoder provides code-first gRPC support — define services as C# interfaces with attributes,
and the framework handles marshalling, method discovery, and client proxy generation. No `.proto` files,
no code generation tools.

## Defining a Service Contract

Decorate an interface with `[ProtoService]` and its methods with `[ProtoMethod]`:

```csharp
using ProtobuffEncoder.Attributes;

[ProtoService("Weather")]
public interface IWeatherGrpcService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<WeatherResponse> GetForecast(WeatherRequest request);

    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    IAsyncEnumerable<WeatherResponse> StreamForecasts(
        WeatherRequest request, CancellationToken ct = default);
}
```

Place service interfaces in a shared contracts project so both server and client reference the same types.

## Method Signature Patterns

Each `ProtoMethodType` expects a specific C# method signature:

| Type | Return | First Parameter | Example |
|------|--------|-----------------|---------|
| `Unary` | `Task<TResponse>` | `TRequest` | `Task<WeatherResponse> Get(WeatherRequest request)` |
| `ServerStreaming` | `IAsyncEnumerable<TResponse>` | `TRequest` | `IAsyncEnumerable<WeatherResponse> Stream(WeatherRequest request, CancellationToken ct)` |
| `ClientStreaming` | `Task<TResponse>` | `IAsyncEnumerable<TRequest>` | `Task<AckResponse> Upload(IAsyncEnumerable<DataChunk> stream, CancellationToken ct)` |
| `DuplexStreaming` | `IAsyncEnumerable<TResponse>` | `IAsyncEnumerable<TRequest>` | `IAsyncEnumerable<NotificationMessage> Chat(IAsyncEnumerable<NotificationMessage> messages, CancellationToken ct)` |

A trailing `CancellationToken` parameter is always optional. The framework extracts it from `ServerCallContext`
on the server side and passes it through on the client side.

## Server Setup

### 1. Implement the interface

```csharp
public class WeatherGrpcServiceImpl : IWeatherGrpcService
{
    public Task<WeatherResponse> GetForecast(WeatherRequest request)
    {
        return Task.FromResult(new WeatherResponse
        {
            City = request.City,
            Forecasts = BuildForecasts(request.Days)
        });
    }

    public async IAsyncEnumerable<WeatherResponse> StreamForecasts(
        WeatherRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < request.Days; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(500, ct);
            yield return new WeatherResponse { City = request.City, Forecasts = [BuildDay(i)] };
        }
    }
}
```

### 2. Register and map

**With the unified setup:**

```csharp
using ProtobuffEncoder.AspNetCore.Setup;

builder.Services.AddProtobuffEncoder()
    .WithGrpc(grpc => grpc
        .UseKestrel(httpPort: 5400, grpcPort: 5401)
        .AddService<WeatherGrpcServiceImpl>());

app.MapProtobufEndpoints();
```

**Or standalone:**

```csharp
using Microsoft.AspNetCore.Server.Kestrel.Core;

builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenLocalhost(5400, o => o.Protocols = HttpProtocols.Http1);
    k.ListenLocalhost(5401, o => o.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();
builder.Services.AddProtobufGrpcService<WeatherGrpcServiceImpl>();

app.MapGrpcService<WeatherGrpcServiceImpl>();
```

Both approaches produce the same result. The unified setup is more concise when registering
multiple transports.

## Client Setup

### Create a typed client proxy

```csharp
using Grpc.Net.Client;
using ProtobuffEncoder.Grpc.Client;

// Direct creation
var channel = GrpcChannel.ForAddress("http://localhost:5401");
var client = channel.CreateProtobufClient<IWeatherGrpcService>();

// Or via Dependency Injection (recommended)
builder.Services.AddProtobufGrpcClient<IWeatherGrpcService>("http://localhost:5401");
```

When using DI, the client is registered as a Singleton and can be injected into your controllers or services:

```csharp
public class MyController(IWeatherGrpcService weatherClient) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var forecast = await weatherClient.GetForecast(new WeatherRequest { City = "Amsterdam" });
        return Ok(forecast);
    }
}
```

`CreateProtobufClient<T>()` returns a `DispatchProxy` that implements your service interface.
Every method call is dispatched through the gRPC channel using `ProtobufMarshaller` for
serialization — the same interface you defined for the server.

### Unary call

```csharp
var response = await client.GetForecast(new WeatherRequest { City = "Amsterdam", Days = 5 });
Console.WriteLine($"{response.City}: {response.Forecasts.Count} days");
```

### Server streaming

```csharp
await foreach (var response in client.StreamForecasts(
    new WeatherRequest { City = "Tokyo", Days = 7 }))
{
    Console.WriteLine($"Day: {response.Forecasts[0].Date}");
}
```

### Duplex streaming

```csharp
async IAsyncEnumerable<NotificationMessage> GenerateMessages()
{
    yield return new NotificationMessage { Source = "Client", Text = "Hello" };
    await Task.Delay(500);
    yield return new NotificationMessage { Source = "Client", Text = "/ping" };
}

await foreach (var reply in chatClient.Chat(GenerateMessages()))
{
    Console.WriteLine($"[{reply.Level}] {reply.Source}: {reply.Text}");
}
```

## How It Works

### Serialization

`ProtobufMarshaller<T>` bridges `ProtobufEncoder.Encode/Decode` into gRPC's `Marshaller<T>`:

```csharp
public static Marshaller<T> Create<T>() where T : class
    => new(
        serializer: msg => ProtobufEncoder.Encode(msg),
        deserializer: bytes => (T)ProtobufEncoder.Decode(typeof(T), bytes));
```

Messages are serialized using the same attribute-driven encoder as all other transports.

### Server method discovery

`ProtobufGrpcServiceMethodProvider<TService>` implements ASP.NET Core's `IServiceMethodProvider<T>`:

1. Scans `TService` for interfaces decorated with `[ProtoService]`
2. Uses `GetInterfaceMap()` to find the implementation methods
3. Extracts `TRequest`/`TResponse` from method signatures
4. Creates `Method<TRequest, TResponse>` descriptors with `ProtobufMarshaller`
5. Binds handler delegates that adapt between user-friendly signatures and gRPC handler delegates

### Client proxy generation

`ProtobufGrpcClientProxy` uses `DispatchProxy` to create runtime interface implementations:

1. At initialization, reflects on the interface to build a handler per method
2. Each handler creates a `Method<TRequest, TResponse>` descriptor (cached)
3. `Invoke()` dispatches to the pre-built handler via dictionary lookup
4. Streaming handlers manage `IAsyncStreamReader`/`IServerStreamWriter` adapters automatically

## gRPC Routes

gRPC methods are exposed at `/{ServiceName}/{MethodName}`:

| Service | Method | Route |
|---------|--------|-------|
| `[ProtoService("Weather")]` | `GetForecast` | `/Weather/GetForecast` |
| `[ProtoService("Weather")]` | `StreamForecasts` | `/Weather/StreamForecasts` |
| `[ProtoService("Chat")]` | `Chat` | `/Chat/Chat` |
| `[ProtoService("Chat")]` | `SendNotification` | `/Chat/SendNotification` |

Override the method name with `[ProtoMethod(ProtoMethodType.Unary, Name = "CustomName")]`.

## HTTP Protocol Configuration

gRPC requires HTTP/2. Without TLS, Kestrel cannot negotiate HTTP/2 via ALPN — so HTTP/1.1
(browser) and HTTP/2 (gRPC) must be served on **separate ports**.

### `UseKestrel(httpPort, grpcPort)`

Configures two Kestrel endpoints:

| Port | Protocol | Purpose |
|------|----------|---------|
| `httpPort` | HTTP/1.1 | Browser dashboard, REST APIs, health checks |
| `grpcPort` | HTTP/2 | gRPC calls from clients |

```csharp
builder.Services.AddProtobuffEncoder()
    .WithGrpc(grpc => grpc
        .UseKestrel(httpPort: 5400, grpcPort: 5401)
        .AddService<WeatherGrpcServiceImpl>());
```

The gRPC client connects to the `grpcPort`:

```csharp
var channel = GrpcChannel.ForAddress("http://localhost:5401");
var client = channel.CreateProtobufClient<IWeatherGrpcService>();
```

### With HTTPS (single port)

When using HTTPS, Kestrel negotiates both HTTP/1.1 and HTTP/2 via ALPN on a single port.
No `UseKestrel()` call is needed — just configure HTTPS in `launchSettings.json` or Kestrel
options:

```csharp
// Both browser and gRPC connect to the same HTTPS endpoint
var channel = GrpcChannel.ForAddress("https://localhost:7400");
```
