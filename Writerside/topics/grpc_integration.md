# gRPC Integration

The `ProtobuffEncoder.Grpc` package enables code-first gRPC services and clients using `[ProtoService]` interfaces, without `.proto` files or code generation. It bridges ProtobuffEncoder into the gRPC pipeline via custom marshallers.

## ProtobufMarshaller

Creates gRPC `Marshaller<T>` instances that use `ProtobufEncoder` for serialization:

```C#
using ProtobuffEncoder.Grpc;

Marshaller<WeatherRequest> marshaller = ProtobufMarshaller.Create<WeatherRequest>();
// Uses ProtobufEncoder.Encode for serialization
// Uses ProtobufEncoder.Decode for deserialization
```

This is the foundation that all other gRPC components build on.

## Defining a Service

Use `[ProtoService]` on an interface and `[ProtoMethod]` on each RPC method:

```C#
using ProtobuffEncoder.Attributes;

[ProtoService("WeatherService")]
public interface IWeatherGrpcService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<WeatherResponse> GetForecast(WeatherRequest request);

    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    IAsyncEnumerable<WeatherUpdate> StreamUpdates(
        WeatherRequest request, CancellationToken ct);

    [ProtoMethod(ProtoMethodType.ClientStreaming)]
    Task<WeatherSummary> UploadReadings(
        IAsyncEnumerable<SensorReading> readings, CancellationToken ct);

    [ProtoMethod(ProtoMethodType.DuplexStreaming)]
    IAsyncEnumerable<Alert> Monitor(
        IAsyncEnumerable<SensorReading> readings, CancellationToken ct);
}
```

### Method Signatures

| Method Type | Parameter | Return Type |
|------------|-----------|-------------|
| `Unary` | `TRequest` | `Task<TResponse>` |
| `ServerStreaming` | `TRequest` + optional `CancellationToken` | `IAsyncEnumerable<TResponse>` |
| `ClientStreaming` | `IAsyncEnumerable<TRequest>` + optional `CancellationToken` | `Task<TResponse>` |
| `DuplexStreaming` | `IAsyncEnumerable<TRequest>` + optional `CancellationToken` | `IAsyncEnumerable<TResponse>` |

## Implementing a Service

```C#
public class WeatherGrpcServiceImpl : IWeatherGrpcService
{
    public Task<WeatherResponse> GetForecast(WeatherRequest request)
    {
        return Task.FromResult(new WeatherResponse
        {
            City = request.City,
            Temperature = 22.5,
            Description = "Sunny"
        });
    }

    public async IAsyncEnumerable<WeatherUpdate> StreamUpdates(
        WeatherRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            yield return new WeatherUpdate { Temperature = Random.Shared.Next(15, 30) };
            await Task.Delay(1000, ct);
        }
    }

    // ... other methods
}
```

## Server Registration

### With Builder Pattern

```C#
builder.Services.AddProtobuffEncoder()
    .WithGrpc(grpc => grpc
        .AddService<WeatherGrpcServiceImpl>()
        .AddService<ChatGrpcServiceImpl>());
```

### Direct Registration

```C#
builder.Services.AddGrpc();
builder.Services.AddProtobufGrpcService<WeatherGrpcServiceImpl>();
```

## Client Proxy

The `ProtobufGrpcClientProxy` creates a `DispatchProxy`-based runtime implementation of any `[ProtoService]` interface.

### Creating a Client

```C#
using Grpc.Net.Client;
using ProtobuffEncoder.Grpc.Client;

var channel = GrpcChannel.ForAddress("http://localhost:5400");
var client = channel.CreateProtobufClient<IWeatherGrpcService>();

// Use like a normal interface
var response = await client.GetForecast(new WeatherRequest { City = "Amsterdam" });
```

### Client Proxy Features

- **Runtime proxy generation** via `DispatchProxy`
- **Pre-built handlers** at initialization time (dictionary lookup on invoke, no reflection)
- **All four method types** supported
- **Automatic marshalling** via `ProtobufMarshaller`
- **CancellationToken** support (extracted from method arguments)
- **No code generation** required

### DI Registration

```C#
builder.Services.AddProtobufGrpcClient<IWeatherGrpcService>(
    channel => GrpcChannel.ForAddress("http://localhost:5400"));
```

## ServiceMethodDescriptor

Internal class that discovers gRPC methods from `[ProtoService]` interfaces via reflection.

### Discovery Modes

| Mode | Use Case |
|------|----------|
| `Discover(Type serviceType)` | From an implementation type (server-side) |
| `Discover(Type interfaceType, isInterfaceOnly: true)` | From an interface only (client-side) |

### Discovered Properties

| Property | Description |
|----------|-------------|
| `ServiceName` | From `[ProtoService("name")]` |
| `MethodName` | From `[ProtoMethod(Name = "...")]` or method name |
| `MethodType` | `Unary`, `ServerStreaming`, `ClientStreaming`, `DuplexStreaming` |
| `RequestType` | Extracted from method parameter |
| `ResponseType` | Extracted from method return type |
| `InterfaceMethod` | The `MethodInfo` on the interface |
| `ImplementationMethod` | The `MethodInfo` on the implementation class |
| `HasCancellationToken` | Whether the method accepts `CancellationToken` |

## Validation

`CreateProtobufClient<T>()` validates:

- `T` must be an interface
- `T` must be decorated with `[ProtoService]`

Both checks throw `ArgumentException` with descriptive messages if violated.

## Complete Example

### Server

```C#
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddProtobuffEncoder()
    .WithGrpc(grpc => grpc.AddService<WeatherGrpcServiceImpl>());

var app = builder.Build();
app.MapGrpcService<WeatherGrpcServiceImpl>();
app.Run();
```

### Client

```C#
var channel = GrpcChannel.ForAddress("http://localhost:5400");
var client = channel.CreateProtobufClient<IWeatherGrpcService>();

// Unary
var forecast = await client.GetForecast(new WeatherRequest { City = "Amsterdam" });

// Server streaming
await foreach (var update in client.StreamUpdates(request, ct))
    Console.WriteLine($"Temp: {update.Temperature}");

// Client streaming
var summary = await client.UploadReadings(GenerateReadingsAsync(), ct);

// Duplex streaming
await foreach (var alert in client.Monitor(GenerateReadingsAsync(), ct))
    Console.WriteLine($"Alert: {alert.Message}");
```
