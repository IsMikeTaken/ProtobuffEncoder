# ASP.NET Core Integration

The `ProtobuffEncoder.AspNetCore` package provides everything needed for API-to-API protobuf communication over HTTP.

## Setup

```csharp
builder.Services.AddControllers().AddProtobufFormatters();
```

This registers:
- `ProtobufInputFormatter` — deserializes `application/x-protobuf` request bodies
- `ProtobufOutputFormatter` — serializes response bodies to `application/x-protobuf`

## Receiving Protobuf Requests

### With formatters (automatic)

When formatters are registered, controller actions and minimal APIs can accept protobuf bodies automatically when the `Content-Type` is `application/x-protobuf`.

### Manual decoding

```csharp
app.MapPost("/api/weather", async (HttpContext ctx) =>
{
    using var ms = new MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms);
    var request = ProtobufEncoder.Decode<WeatherRequest>(ms.ToArray());

    var response = new WeatherResponse { City = request.City };
    var bytes = ProtobufEncoder.Encode(response);
    return Results.Bytes(bytes, "application/x-protobuf");
});
```

## Sending Protobuf Requests

### HttpClient Extensions

```csharp
using ProtobuffEncoder.AspNetCore;

// POST with protobuf body, receive and deserialize protobuf response
var response = await httpClient.PostProtobufAsync<WeatherRequest, WeatherResponse>(
    "/api/weather", request);

// POST with protobuf body (fire-and-forget, no deserialized response)
await httpClient.PostProtobufAsync("/api/notify", notification);

// GET with protobuf response
var data = await httpClient.GetProtobufAsync<StatusResponse>("/api/status");
```

### ProtobufHttpContent

For manual `HttpClient` usage:

```csharp
using var content = new ProtobufHttpContent(myObject);
// content.Headers.ContentType is "application/x-protobuf"
var response = await httpClient.PostAsync("/api/endpoint", content);
```

### Media Type Constant

```csharp
using ProtobuffEncoder.AspNetCore;

string mediaType = ProtobufMediaType.Protobuf; // "application/x-protobuf"
```

## Shared Contracts Pattern

Define message types in a shared project that both sender and receiver reference:

```
src/
├── ProtobuffEncoder.Contracts/          # Shared types
│   ├── WeatherRequest.cs
│   ├── WeatherResponse.cs
│   └── protos/                          # Auto-generated .proto schemas
│       └── contracts.proto
├── Sender.Api/                          # References Contracts
└── Receiver.Api/                        # References Contracts (or uses schema-only)
```

### Sender setup

```csharp
builder.Services.AddHttpClient("ReceiverApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5100");
});

app.MapGet("/api/send-weather", async (
    string city, int days,
    IHttpClientFactory httpClientFactory) =>
{
    var client = httpClientFactory.CreateClient("ReceiverApi");
    var request = new WeatherRequest { City = city, Days = days };

    var response = await client.PostProtobufAsync<WeatherRequest, WeatherResponse>(
        "/api/weather", request);

    return Results.Ok(new { response.City, response.Forecasts });
});
```

### Schema-only receiver (no Contracts reference)

See [Schema Guide](../guides/schema.md) for the full pattern of receiving and decoding protobuf without any compile-time knowledge of the sender's C# types.
