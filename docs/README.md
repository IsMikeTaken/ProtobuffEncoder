# ProtobuffEncoder

A lightweight, attribute-driven .NET library that serializes and deserializes C# objects to [Protocol Buffer](https://protobuf.dev/programming-guides/encoding/) binary wire format — no `.proto` files or code generation required.

## Features

- **Attribute-based** — mark classes with `[ProtoContract]` and optionally override fields with `[ProtoField]`
- **Auto-mapping** — public properties are included by default with auto-assigned, collision-free field numbers
- **Complex types** — arrays, `List<T>`, `ICollection<T>`, `HashSet<T>`, nullable value types, enums, nested messages
- **Packed encoding** — scalar collections (int[], List\<double\>, etc.) use proto3 packed wire format
- **Streaming** — length-delimited read/write APIs for sending multiple messages over a single stream
- **Async** — full `async`/`await` and `IAsyncEnumerable<T>` support
- **Static messages** — pre-compile encode/decode delegates per type to eliminate cold-start reflection overhead
- **Transport** — `ProtobufSender<T>` and `ProtobufReceiver<T>` for stream-based communication
- **ASP.NET Core** — input/output formatters, `ProtobufHttpContent`, and `HttpClient` extensions for API-to-API protobuf communication

## Quick start

```csharp
using ProtobuffEncoder;
using ProtobuffEncoder.Attributes;

[ProtoContract]
public class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

// Encode
byte[] bytes = ProtobufEncoder.Encode(new Person { Name = "Alice", Age = 30 });

// Decode
var person = ProtobufEncoder.Decode<Person>(bytes);
```

## Attributes

### `[ProtoContract]`

Applied to a class or struct to opt it into protobuf serialization.

| Property         | Type   | Default | Description |
|------------------|--------|---------|-------------|
| `ExplicitFields` | `bool` | `false` | When `true`, only properties marked with `[ProtoField]` are included. When `false`, all public properties are auto-included. |

```csharp
[ProtoContract(ExplicitFields = true)]
public class Sensor
{
    [ProtoField(FieldNumber = 1)]
    public int Id { get; set; }

    // Not serialized because ExplicitFields = true and no [ProtoField]
    public string DebugInfo { get; set; } = "";
}
```

### `[ProtoField]`

Applied to a property to override its protobuf metadata.

| Property       | Type        | Default       | Description |
|----------------|-------------|---------------|-------------|
| `FieldNumber`  | `int`       | auto-assigned | The protobuf field number (1-based). |
| `Name`         | `string?`   | property name | Override the schema field name. |
| `WireType`     | `WireType?` | inferred      | Force a specific wire type. |
| `WriteDefault` | `bool`      | `false`       | Write the field even when it holds its type's default value. |

```csharp
[ProtoContract]
public class Event
{
    [ProtoField(FieldNumber = 10, Name = "event_name", WriteDefault = true)]
    public string Name { get; set; } = "";
}
```

### `[ProtoIgnore]`

Excludes a property from serialization entirely.

```csharp
[ProtoContract]
public class User
{
    public string Name { get; set; } = "";

    [ProtoIgnore]
    public string PasswordHash { get; set; } = "";
}
```

## Field numbering

By default, all public properties (except `[ProtoIgnore]`) are assigned incrementing field numbers starting at 1, based on declaration order. Explicitly assigned numbers are reserved first, and auto-assignment skips them to prevent collisions.

```csharp
[ProtoContract]
public class Example
{
    public string A { get; set; } = "";          // auto → field 1

    [ProtoField(FieldNumber = 5)]
    public string B { get; set; } = "";          // explicit → field 5

    public int C { get; set; }                   // auto → field 2
    public int D { get; set; }                   // auto → field 3
}
```

## Type mapping

| CLR Type                          | Wire Type         | Notes |
|-----------------------------------|-------------------|-------|
| `bool`, `byte`, `sbyte`          | Varint            |       |
| `short`, `ushort`, `int`, `uint` | Varint            |       |
| `enum`                           | Varint            | Underlying integer value |
| `long`, `ulong`                  | Fixed64           |       |
| `double`                         | Fixed64           |       |
| `float`                          | Fixed32           |       |
| `string`                         | LengthDelimited   | UTF-8 encoded |
| `byte[]`                         | LengthDelimited   | Raw bytes |
| `T?` (nullable value type)       | Same as `T`       | Null values are skipped |
| Nested `[ProtoContract]` object  | LengthDelimited   | Recursively encoded |
| `T[]`, `List<T>`, `ICollection<T>`, `HashSet<T>`, etc. | LengthDelimited | See Collections |

## Collections

Any property whose type implements `IEnumerable<T>` (excluding `string` and `byte[]`) is treated as a **repeated field**.

**Packed encoding** is used for scalar element types (`int`, `double`, `bool`, `enum`, etc.) — all values are concatenated into a single length-delimited blob, matching proto3 packed repeated fields.

**Non-packed** (one tag per element) is used for `string`, `byte[]`, and nested message element types.

Supported collection types for decoding: `T[]`, `List<T>`, `IList<T>`, `ICollection<T>`, `IReadOnlyList<T>`, `HashSet<T>`, `ISet<T>`.

```csharp
[ProtoContract]
public class Metrics
{
    public List<string> Labels { get; set; } = [];     // repeated, non-packed
    public int[] Values { get; set; } = [];             // repeated, packed
    public List<DataPoint> Points { get; set; } = [];   // repeated nested messages
}
```

## Nullable types

Nullable value types (`int?`, `double?`, `bool?`, etc.) are fully supported. When the value is `null`, the field is omitted from the output. On decode, the property retains its default (`null`).

```csharp
[ProtoContract]
public class Reading
{
    public double? Temperature { get; set; }   // omitted when null
    public int? SensorId { get; set; }
}
```

## Streaming (delimited messages)

To send or receive multiple messages over a single stream (e.g. a TCP socket or file), use **length-delimited** encoding. Each message is prefixed with a varint-encoded byte length so the reader knows where one message ends and the next begins.

### Writing

```csharp
using var stream = new MemoryStream();

// Synchronous
ProtobufEncoder.WriteDelimitedMessage(person1, stream);
ProtobufEncoder.WriteDelimitedMessage(person2, stream);

// Asynchronous
await ProtobufEncoder.WriteDelimitedMessageAsync(person3, stream, cancellationToken);
```

### Reading

```csharp
stream.Position = 0;

// Read one message (returns null at end of stream)
var msg = ProtobufEncoder.ReadDelimitedMessage<Person>(stream);

// Read all messages as IEnumerable<T>
foreach (var person in ProtobufEncoder.ReadDelimitedMessages<Person>(stream))
{
    Console.WriteLine(person.Name);
}

// Async streaming with IAsyncEnumerable<T>
await foreach (var person in ProtobufEncoder.ReadDelimitedMessagesAsync<Person>(stream, cancellationToken))
{
    Console.WriteLine(person.Name);
}
```

## Static messages (pre-compiled)

For hot paths where you encode/decode the same type many times, create a **static message** to eagerly resolve and cache all type metadata upfront:

```csharp
// Create once, reuse many times
var message = ProtobufEncoder.CreateStaticMessage<Person>();

byte[] bytes = message.Encode(person);
Person decoded = message.Decode(bytes);

// Also supports delimited streaming
message.WriteDelimited(person, stream);
Person? next = message.ReadDelimited(stream);
```

You can also get standalone delegates:

```csharp
Func<Person, byte[]> encode = ProtobufEncoder.CreateStaticEncoder<Person>();
Func<byte[], Person> decode = ProtobufEncoder.CreateStaticDecoder<Person>();
```

## Encode to / decode from streams

Besides `byte[]`, you can encode directly into any `Stream`:

```csharp
// Sync
ProtobufEncoder.Encode(person, networkStream);

// Async
await ProtobufEncoder.EncodeAsync(person, networkStream, cancellationToken);

// Decode from stream (reads all remaining bytes)
var result = await ProtobufEncoder.DecodeAsync<Person>(networkStream, cancellationToken);
```

## Full example

```csharp
using ProtobuffEncoder;
using ProtobuffEncoder.Attributes;

public enum ContactType { Unknown, Personal, Work }

[ProtoContract]
public class Person
{
    public string Name { get; set; } = "";

    [ProtoField(FieldNumber = 10, Name = "email_address")]
    public string Email { get; set; } = "";

    public int Age { get; set; }
    public double? Score { get; set; }
    public ContactType Type { get; set; }
    public List<string> Tags { get; set; } = [];
    public int[] LuckyNumbers { get; set; } = [];
    public Address? HomeAddress { get; set; }
    public List<PhoneNumber> PhoneNumbers { get; set; } = [];

    [ProtoIgnore]
    public string InternalNotes { get; set; } = "";
}

[ProtoContract]
public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public int ZipCode { get; set; }
}

[ProtoContract]
public class PhoneNumber
{
    public string Number { get; set; } = "";
    public ContactType Type { get; set; }
}

// Usage
var person = new Person
{
    Name = "Alice",
    Email = "alice@example.com",
    Age = 30,
    Score = 98.5,
    Type = ContactType.Work,
    Tags = ["developer", "lead"],
    LuckyNumbers = [7, 13, 42],
    HomeAddress = new Address { Street = "123 Main St", City = "Springfield", ZipCode = 62701 },
    PhoneNumbers =
    [
        new PhoneNumber { Number = "+1-555-0100", Type = ContactType.Work },
        new PhoneNumber { Number = "+1-555-0101", Type = ContactType.Personal }
    ]
};

byte[] encoded = ProtobufEncoder.Encode(person);
var decoded = ProtobufEncoder.Decode<Person>(encoded);
```

## Transport: Sender & Receiver

For stream-based communication (TCP sockets, named pipes, file streams), use the typed `ProtobufSender<T>` and `ProtobufReceiver<T>` wrappers. They handle length-delimited framing automatically.

```csharp
using ProtobuffEncoder.Transport;

// Sender side
await using var sender = new ProtobufSender<Person>(networkStream);
await sender.SendAsync(person);
await sender.SendManyAsync(people);

// Receiver side
await using var receiver = new ProtobufReceiver<Person>(networkStream);

// Read one
var msg = receiver.Receive();

// Async stream
await foreach (var person in receiver.ReceiveAllAsync(cancellationToken))
{
    Console.WriteLine(person.Name);
}

// Or use a listener callback
await receiver.ListenAsync(async person =>
{
    Console.WriteLine($"Received: {person.Name}");
}, cancellationToken);
```

## ASP.NET Core integration

The `ProtobuffEncoder.AspNetCore` package provides everything needed for API-to-API protobuf communication over HTTP.

### Setup

```csharp
// In your API's Program.cs
builder.Services.AddControllers().AddProtobufFormatters();
```

This registers `ProtobufInputFormatter` and `ProtobufOutputFormatter` so your API can accept and return `application/x-protobuf` bodies.

### Receiving protobuf requests (server side)

```csharp
app.MapPost("/api/weather", async (HttpContext ctx) =>
{
    // Read protobuf request body
    using var ms = new MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms);
    var request = ProtobufEncoder.Decode<WeatherRequest>(ms.ToArray());

    // Build response
    var response = new WeatherResponse { City = request.City, ... };

    // Return as protobuf
    var bytes = ProtobufEncoder.Encode(response);
    return Results.Bytes(bytes, "application/x-protobuf");
});
```

### Sending protobuf requests (client side)

Use the `HttpClient` extension methods:

```csharp
using ProtobuffEncoder.AspNetCore;

// POST with protobuf body, receive protobuf response
var response = await httpClient.PostProtobufAsync<WeatherRequest, WeatherResponse>(
    "/api/weather", request);

// POST with protobuf body (no deserialized response)
await httpClient.PostProtobufAsync("/api/notify", notification);

// GET with protobuf response
var data = await httpClient.GetProtobufAsync<StatusResponse>("/api/status");
```

### ProtobufHttpContent

For manual HttpClient usage:

```csharp
using var content = new ProtobufHttpContent(myObject);
var response = await httpClient.PostAsync("/api/endpoint", content);
```

### Shared contracts

Define your message types in a shared project that both APIs reference:

```csharp
// ProtobuffEncoder.Contracts/WeatherRequest.cs
[ProtoContract]
public class WeatherRequest
{
    public string City { get; set; } = "";
    public int Days { get; set; }
}
```

## Two-API example

The solution includes a working example of two APIs communicating via protobuf:

**Receiver API** (`http://localhost:5100`) — accepts protobuf requests and returns protobuf responses:
- `POST /api/weather` — accepts a `WeatherRequest`, returns a `WeatherResponse`
- `POST /api/notifications` — accepts a `NotificationMessage`, returns an `AckResponse`

**Sender API** (`http://localhost:5200`) — sends protobuf to the Receiver and exposes JSON endpoints for testing:
- `GET /api/send-weather?city=Amsterdam&days=3` — fetches weather from Receiver via protobuf
- `POST /api/send-notification` — forwards a JSON notification to Receiver as protobuf

```bash
# Start both APIs
dotnet run --project ProtobuffEncoder.Api.Receiver
dotnet run --project ProtobuffEncoder.Api.Sender

# Test: Sender calls Receiver with protobuf, returns JSON
curl "http://localhost:5200/api/send-weather?city=Amsterdam&days=3"

curl -X POST http://localhost:5200/api/send-notification \
  -H "Content-Type: application/json" \
  -d '{"source":"Monitor","text":"CPU high","level":"Warning","tags":["infra"]}'
```

## Proto schema generation

The library can auto-generate `.proto` schema files from your `[ProtoContract]` types. These schemas serve as a **source of truth** — they can be shared to any consumer (even one with zero knowledge of your C# classes) so it can decode your messages.

### Generate with the CLI tool

```bash
# Generate .proto files from a compiled assembly and auto-append to the .csproj
dotnet run --project ProtobuffEncoder.Tool -- \
  "ProtobuffEncoder.Contracts/bin/Debug/net10.0/ProtobuffEncoder.Contracts.dll" \
  "ProtobuffEncoder.Contracts/protos" \
  "ProtobuffEncoder.Contracts/ProtobuffEncoder.Contracts.csproj"
```

This:
1. Scans the assembly for all `[ProtoContract]` types
2. Generates `.proto` files in the output directory (one per namespace)
3. Auto-appends `<Content Include="protos\*.proto">` to the `.csproj` with `CopyToOutputDirectory`

### Generate programmatically

```csharp
using ProtobuffEncoder.Schema;

// Single type
string proto = ProtoSchemaGenerator.Generate(typeof(WeatherRequest));

// All types in an assembly, written to disk
var paths = ProtoSchemaGenerator.GenerateToDirectory(assembly, "protos/");
```

### Generated output example

```proto
syntax = "proto3";

package ProtobuffEncoder.Contracts;

enum NotificationLevel {
  Info = 0;
  Warning = 1;
  Error = 2;
}

message WeatherRequest {
  string City = 1;
  int32 Days = 2;
  bool IncludeHourly = 3;
}

message DayForecast {
  string Date = 1;
  double TemperatureMin = 2;
  double TemperatureMax = 3;
  string Condition = 4;
  int32 HumidityPercent = 5;
  optional double WindSpeed = 6;
}

message WeatherResponse {
  string City = 1;
  repeated DayForecast Forecasts = 2;
  int64 GeneratedAtUtc = 3;
}
```

### MSBuild integration

Import the targets file in your `.csproj` to auto-generate on build:

```xml
<Import Project="..\ProtobuffEncoder\build\ProtobuffEncoder.targets" />
```

Configure with properties:
```xml
<PropertyGroup>
  <ProtoOutputDir>protos</ProtoOutputDir>           <!-- default: protos -->
  <GenerateProtoOnBuild>true</GenerateProtoOnBuild>  <!-- default: true -->
</PropertyGroup>
```

## Schema-based decoding (no C# types)

A receiver that has **no reference to your Contracts project** can still decode messages using only the `.proto` schema files. This is the same pattern as metric/telemetry protos — share the `.proto` files, decode anywhere.

### SchemaDecoder

```csharp
using ProtobuffEncoder.Schema;

// Load schemas from a directory of .proto files
var decoder = SchemaDecoder.FromDirectory("protos/");

// Or from a single file or raw content
var decoder2 = SchemaDecoder.FromFile("protos/contracts.proto");
var decoder3 = SchemaDecoder.FromProtoContent(protoString);

// Decode binary protobuf → DecodedMessage (dictionary-like)
DecodedMessage msg = decoder.Decode("WeatherRequest", bytes);

string city = msg.Get<string>("City");           // "Amsterdam"
long days = msg.Get<long>("Days");               // 3
List<string> tags = msg.GetRepeated<string>("Tags");
DecodedMessage nested = msg.GetMessage("HomeAddress");
List<DecodedMessage> items = msg.GetMessages("Forecasts");
```

### ProtobufWriter (build responses without C# types)

```csharp
using ProtobuffEncoder.Schema;

// Build a protobuf message using field numbers from the .proto schema
var writer = new ProtobufWriter();
writer.WriteString(1, "Amsterdam");              // City = 1
writer.WriteVarint(2, 3);                        // Days = 2
writer.WriteBool(3, true);                       // IncludeHourly = 3

// Nested messages
var forecast = new ProtobufWriter();
forecast.WriteString(1, "2026-03-11");
forecast.WriteDouble(2, 5.3);
forecast.WriteDouble(3, 14.8);

var response = new ProtobufWriter();
response.WriteString(1, "Amsterdam");
response.WriteRepeatedMessage(2, [forecast]);    // repeated DayForecast
response.WriteFixed64(3, timestamp);

byte[] bytes = response.ToByteArray();
```

### Receiver API without Contracts reference

The Receiver API demonstrates this pattern — it has **zero compile-time dependency** on `ProtobuffEncoder.Contracts`. It only gets the `.proto` files copied at build time:

```xml
<!-- ProtobuffEncoder.Api.Receiver.csproj -->
<ItemGroup>
  <ProjectReference Include="..\ProtobuffEncoder.AspNetCore\ProtobuffEncoder.AspNetCore.csproj" />
  <!-- NO reference to Contracts — schema-driven decoding only -->
</ItemGroup>

<!-- Copy .proto schemas from Contracts as the source of truth -->
<ItemGroup>
  <Content Include="..\ProtobuffEncoder.Contracts\protos\**\*.proto"
           Link="protos\%(Filename)%(Extension)">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

```csharp
// At startup — register SchemaDecoder from the .proto files
var protoDir = Path.Combine(AppContext.BaseDirectory, "protos");
builder.Services.AddSingleton(_ => SchemaDecoder.FromDirectory(protoDir));

// In an endpoint — decode and respond using only schema knowledge
app.MapPost("/api/weather", async (HttpContext ctx, SchemaDecoder schema) =>
{
    var bytes = await ReadBodyAsync(ctx);
    var request = schema.Decode("WeatherRequest", bytes);

    string city = request.Get<string>("City") ?? "Unknown";
    // ... build response with ProtobufWriter ...
});
```

## Project structure

```
ProtobuffEncoder/                   # Core library
├── Attributes/
│   ├── ProtoContractAttribute.cs
│   ├── ProtoFieldAttribute.cs
│   └── ProtoIgnoreAttribute.cs
├── Schema/
│   ├── ProtoSchemaGenerator.cs    # [ProtoContract] → .proto files
│   ├── ProtoSchemaParser.cs       # .proto files → schema model
│   ├── ProtoSchema.cs             # Schema model (ProtoFile, ProtoMessageDef, etc.)
│   ├── SchemaDecoder.cs           # Decode binary protobuf using only .proto schemas
│   ├── DecodedMessage.cs          # Dictionary-like decode result
│   └── ProtobufWriter.cs          # Build protobuf messages by field number
├── Transport/
│   ├── ProtobufSender.cs
│   └── ProtobufReceiver.cs
├── build/
│   └── ProtobuffEncoder.targets   # MSBuild integration for auto-generation
├── ContractResolver.cs
├── FieldDescriptor.cs
├── ProtobufEncoder.cs
├── StaticMessage.cs
└── WireType.cs

ProtobuffEncoder.Tool/              # CLI tool for .proto generation
└── Program.cs                      # Scans assemblies, generates protos, updates csproj

ProtobuffEncoder.AspNetCore/        # ASP.NET Core integration
├── HttpClientExtensions.cs
├── ProtobufHttpContent.cs
├── ProtobufInputFormatter.cs
├── ProtobufOutputFormatter.cs
├── ProtobufMediaType.cs
└── ServiceCollectionExtensions.cs

ProtobuffEncoder.Contracts/         # Shared message types (source of truth)
├── protos/                         # Auto-generated .proto schemas
│   └── protobuffencoder_contracts.proto
├── WeatherRequest.cs
├── WeatherResponse.cs
└── NotificationMessage.cs

ProtobuffEncoder.Api.Receiver/      # Receives protobuf — NO Contracts reference
└── Program.cs                      # Uses SchemaDecoder + ProtobufWriter

ProtobuffEncoder.Api.Sender/        # Sends protobuf — references Contracts
└── Program.cs
```

## Limitations

- No `map<K,V>` support (use a `List<T>` of key-value pair messages as a workaround)
- No `oneof` fields
- No ZigZag encoding for signed integers (`sint32`/`sint64`) — negative values use full 10-byte varint
- No proto2 groups (deprecated in proto3)
- Reflection-based — for maximum performance in AOT scenarios, consider source generators
