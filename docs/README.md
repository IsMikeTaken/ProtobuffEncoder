# Proto ~ Buffed

**ProtobuffEncoder** is a high-performance, zero-dependency protobuf serialization framework for .NET 8, 9, and 10. It provides a complete pipeline from C# attribute-driven contracts to binary wire-format encoding, streaming transport, validation, ASP.NET Core integration, gRPC services, WebSocket real-time communication, and `.proto` schema generation -- all without requiring Google.Protobuf or `protoc`.

## Architecture Overview

```
  C# Classes + Attributes
         |
    ContractResolver         (reflection-based descriptor caching)
         |
    ProtobufEncoder          (encode / decode / streaming)
         |
  +------+------+------+------+
  |      |      |      |      |
 REST   gRPC  WebSocket Schema  CLI Tool
  |      |      |      |        |
 ASP.NET Grpc  WS     .proto   protobuf-encoder
 Core   Channel Client  Gen     (dotnet tool)
```

## Packages

| Package | Description | Targets |
|---------|-------------|---------|
| `ProtobuffEncoder` | Core encoder, decoder, transport, validation, schema | net8.0, net9.0, net10.0 |
| `ProtobuffEncoder.AspNetCore` | MVC formatters, HttpClient extensions, builder setup | net8.0, net9.0, net10.0 |
| `ProtobuffEncoder.Grpc` | Code-first gRPC marshaller, client proxy, service discovery | net8.0, net9.0, net10.0 |
| `ProtobuffEncoder.WebSockets` | Managed WebSocket client/server, connection manager, retry | net8.0, net9.0, net10.0 |
| `ProtobuffEncoder.Contracts` | Example contracts and service interfaces | net8.0, net9.0, net10.0 |
| `ProtobuffEncoder.Tool` | CLI tool for `.proto` generation and `.csproj` patching | net10.0 |

## Quick Start

### 1. Define a Contract

```csharp
[ProtoContract]
public class WeatherRequest
{
    [ProtoField(1)] public string City { get; set; } = "";
    [ProtoField(2)] public int Days { get; set; }
}
```

### 2. Encode and Decode

```csharp
var request = new WeatherRequest { City = "Amsterdam", Days = 5 };
byte[] bytes = ProtobufEncoder.Encode(request);
WeatherRequest decoded = ProtobufEncoder.Decode<WeatherRequest>(bytes);
```

### 3. Stream Over Transport

```csharp
await using var sender = new ProtobufSender<WeatherRequest>(networkStream);
await sender.SendAsync(request);
```

### 4. Generate .proto Schema

```csharp
string proto = ProtoSchemaGenerator.Generate(typeof(WeatherRequest));
```

## Supported Types

| Category | Types |
|----------|-------|
| **Integers** | `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `nint`, `nuint` |
| **Floating point** | `float`, `double`, `Half`, `decimal` |
| **Boolean** | `bool` |
| **Text** | `string` |
| **Binary** | `byte[]` |
| **Date/Time** | `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly` |
| **Identifiers** | `Guid`, `Uri`, `Version` |
| **Large numbers** | `Int128`, `UInt128`, `BigInteger`, `Complex` |
| **Enums** | Any `enum` type |
| **Collections** | `List<T>`, `T[]`, `IList<T>`, `ICollection<T>`, `HashSet<T>`, `ISet<T>` |
| **Dictionaries** | `Dictionary<K,V>`, `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>` |
| **Nested messages** | Any class with `[ProtoContract]` or implicit nesting |
| **Nullable** | `T?` for all value types |

## Test Coverage

**430+ tests** across 5 test projects using FIRST-U Pass/Fail testing patterns:

| Test Project | Tests | Coverage Area |
|-------------|-------|---------------|
| ProtobuffEncoder.Tests | 200+ | Core encoder, decoder, streaming, validation, schema, attributes |
| ProtobuffEncoder.AspNetCore.Tests | 41 | Formatters, HttpClient, setup, DI integration |
| ProtobuffEncoder.Grpc.Tests | 34 | Marshaller, service discovery, client proxy, extensions |
| ProtobuffEncoder.WebSockets.Tests | 117 | Client, server, connection manager, retry, stream |
| ProtobuffEncoder.Tool.Tests | 12 | Project modifier, csproj patching |

## Benchmarks

Performance tested across .NET 8, 9, and 10 with **15 benchmark suites** covering:

- Core encode/decode (small and large payloads)
- Collection serialization (lists, maps)
- Static message pre-compiled delegates
- Streaming (length-delimited batches)
- Duplex stream send/receive
- Validation pipeline throughput
- Schema generation and parsing
- ProtobufWriter low-level API
- Payload scaling (100B to 100KB)
- Nested object depth (1-3 levels)
- OneOf union encoding
- Inheritance (ProtoInclude)
- Async streaming
- ContractResolver caching

### EncoderBenchmarks (net10.0)

```
BenchmarkDotNet v0.15.8, Windows 11
12th Gen Intel Core i9-12900H, .NET 10.0.5

| Method       | Mean     | Gen0   | Allocated |
|------------- |---------:|-------:|----------:|
| Encode_Small | 602.6 ns | 0.0610 |     792 B |
| Decode_Small | 529.1 ns | 0.0572 |     736 B |
| Encode_Large | 999.0 ns | 0.5417 |    6832 B |
| Decode_Large | 820.5 ns | 0.3052 |    3832 B |
```

### CollectionBenchmarks (net10.0)

```
| Method             | Mean     | Gen0   | Allocated |
|------------------- |---------:|-------:|----------:|
| Encode_Collections | 2.518 us | 0.4921 |   6.07 KB |
| Decode_Collections | 4.039 us | 0.9460 |  11.67 KB |
```

## Documentation

| Topic | Description |
|-------|-------------|
| [Guides: Attributes](guides/attributes.md) | Complete attribute reference |
| [Guides: Serialization](guides/serialization.md) | Wire format, type mapping |
| [Guides: Transport](guides/transport.md) | Sender, Receiver, DuplexStream |
| [Guides: Schema](guides/schema.md) | ProtoSchemaGenerator, imports, services |
| [Guides: WebSockets](guides/websockets.md) | Client, server, retry |
| [Guides: gRPC](guides/grpc.md) | Marshaller, client proxy |
| [Guides: Setup](guides/setup.md) | ASP.NET Core builder |
| [API: ASP.NET Core](api/aspnetcore.md) | Formatters, HttpClient |
| [API: Tool](api/tool.md) | CLI tool reference |
| [Testing](test_strategy.md) | FIRST-U patterns, test matrix |
| [Demos](demos/README.md) | Demo project walkthroughs |
