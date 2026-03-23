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
| `ProtobuffEncoder.Tool` | CLI tool for `.proto` generation and `.csproj` patching | net8.0, net9.0, net10.0 |

## Quick Start

### 1. Define a Contract

```C#
[ProtoContract]
public class WeatherRequest
{
    [ProtoField(1)] public string City { get; set; } = "";
    [ProtoField(2)] public int Days { get; set; }
}
```

### 2. Encode and Decode

```C#
var request = new WeatherRequest { City = "Amsterdam", Days = 5 };
byte[] bytes = ProtobufEncoder.Encode(request);
WeatherRequest decoded = ProtobufEncoder.Decode<WeatherRequest>(bytes);
```

### 3. Stream Over Transport

```C#
await using var sender = new ProtobufSender<WeatherRequest>(networkStream);
await sender.SendAsync(request);
```

### 4. Generate .proto Schema

```C#
string proto = ProtoSchemaGenerator.Generate(typeof(WeatherRequest));
// syntax = "proto3";
// message WeatherRequest {
//   string City = 1;
//   int32 Days = 2;
// }
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

## Documentation

### Core

| Topic | Description |
|-------|-------------|
| [Getting Started](getting_started.md) | Installation, first contract, encode/decode |
| [Attributes Reference](attributes_reference.md) | All 8 attributes with properties and examples |
| [Serialization Deep Dive](serialization_deep_dive.md) | Wire format, type mapping, encoding steps |
| [Auto-Discovery](auto_discovery.md) | ProtoRegistry, field numbering strategies, assembly scanning |
| [Value Encoding](value_encoding.md) | ProtoEncoding, ProtoValue, ProtoMessage, emoji support |
| [Transport Layer](transport_layer.md) | Sender, Receiver, DuplexStream, ValueSender/Receiver |
| [Validation Pipeline](validation_pipeline.md) | ValidationPipeline, validated transport |

### Schema

| Topic | Description |
|-------|-------------|
| [Schema Generation](schema_generation.md) | ProtoSchemaGenerator, imports, services |
| [Schema Decoding](schema_decoding.md) | ProtoSchemaParser, SchemaDecoder, ProtobufWriter |

### Integrations

| Topic | Description |
|-------|-------------|
| [ASP.NET Core](aspnetcore_integration.md) | Builder pattern, REST formatters, HttpClient |
| [gRPC](grpc_integration.md) | Marshaller, client proxy, service discovery |
| [WebSockets](websocket_integration.md) | Client, server, connection manager, retry |

### Tools & Quality

| Topic | Description |
|-------|-------------|
| [CLI Tool](cli_tool.md) | Schema generation, csproj patching |
| [Test Strategy](test_strategy_overview.md) | FIRST-U patterns, 430+ tests |
| [Benchmarks](benchmarks_overview.md) | Performance results across .NET 8/9/10 |
| [Demos](demos_overview.md) | Demo project walkthroughs |
