# ProtobuffEncoder

A lightweight, attribute-driven .NET library that serializes and deserializes C# objects to [Protocol Buffer](https://protobuf.dev/programming-guides/encoding/) binary wire format — no `.proto` files or code generation required.

## Features

- **Attribute-based** — mark classes with `[ProtoContract]` and optionally override fields with `[ProtoField]`
- **Auto-mapping** — public properties are included by default with auto-assigned, collision-free field numbers
- **Complex types** — arrays, `List<T>`, `Dictionary<K,V>`, nullable value types, enums, nested messages, inheritance
- **Advanced attributes** — `[ProtoMap]` for dictionaries, `[ProtoOneOf]` for unions, `[ProtoInclude]` for polymorphism
- **Packed encoding** — scalar collections use proto3 packed wire format
- **Streaming** — length-delimited framing for multi-message streams
- **Bi-directional** — `ProtobufDuplexStream<TSend, TReceive>` for full-duplex communication
- **Validation** — `ValidationPipeline<T>` with configurable rules on send/receive
- **Async** — full `async`/`await` and `IAsyncEnumerable<T>` support
- **Static messages** — pre-compiled encode/decode delegates to eliminate reflection overhead
- **Schema generation** — auto-generate `.proto` files from C# types
- **Schema decoding** — decode protobuf binary using only `.proto` schemas, no C# types needed
- **ASP.NET Core** — input/output formatters and `HttpClient` extensions
- **Multi-target** — supports .NET 10, .NET 9, and .NET 8

## Quick Start

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

## Documentation

| Guide | Description |
|-------|-------------|
| [Attributes](guides/attributes.md) | All attributes: `[ProtoContract]`, `[ProtoField]`, `[ProtoIgnore]`, `[ProtoMap]`, `[ProtoOneOf]`, `[ProtoInclude]` |
| [Serialization](guides/serialization.md) | Type mapping, field numbering, collections, nullable types, streaming, static messages |
| [Transport](guides/transport.md) | `ProtobufSender`, `ProtobufReceiver`, `ProtobufDuplexStream`, validation pipelines |
| [Schema](guides/schema.md) | Proto schema generation, parsing, schema-based decoding, `ProtobufWriter` |
| [ASP.NET Core](api/aspnetcore.md) | Formatters, `HttpClient` extensions, `ProtobufHttpContent` |
| [CLI Tool](api/tool.md) | `ProtobuffEncoder.Tool` usage, MSBuild integration |
| [Demos](demos/README.md) | Running the interactive demo applications |

## Project Structure

```
ProtobuffEncoder/
├── src/
│   ├── ProtobuffEncoder/                    Core library
│   │   ├── Attributes/                      Serialization attributes
│   │   ├── Schema/                          Proto generation, parsing, decoding
│   │   ├── Transport/                       Sender, receiver, duplex, validation
│   │   └── build/                           MSBuild targets
│   ├── ProtobuffEncoder.AspNetCore/         ASP.NET Core integration
│   └── ProtobuffEncoder.Contracts/          Shared contract types + generated .proto
│
├── tools/
│   └── ProtobuffEncoder.Tool/              CLI for .proto generation
│
├── demos/
│   ├── ProtobuffEncoder.Demo.Api.Sender/           HTTP sender (port 5200)
│   ├── ProtobuffEncoder.Demo.Api.Receiver/          Schema-only receiver (port 5100)
│   ├── ProtobuffEncoder.Demo.Bidirectional.Server/  WebSocket server (port 5300)
│   ├── ProtobuffEncoder.Demo.Bidirectional.Client/  WebSocket console client
│   └── ProtobuffEncoder.Demo.Console/               Feature showcase
│
└── docs/
    ├── guides/                              In-depth guides
    ├── api/                                 API & tooling reference
    └── demos/                               Demo documentation
```

## Supported .NET Versions

The core library, ASP.NET Core integration, and CLI tool all multi-target:

| Package | net10.0 | net9.0 | net8.0 |
|---------|---------|--------|--------|
| ProtobuffEncoder | yes | yes | yes |
| ProtobuffEncoder.AspNetCore | yes | yes | yes |
| ProtobuffEncoder.Tool | yes | yes | yes |

## License

MIT
