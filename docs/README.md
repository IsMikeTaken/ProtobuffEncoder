# ProtobuffEncoder

A lightweight, attribute-driven .NET library that serializes and deserializes C# objects to [Protocol Buffer](https://protobuf.dev/programming-guides/encoding/) binary wire format — no `.proto` files or code generation required.

## Features

- **Attribute-based** — mark classes with `[ProtoContract]` and optionally override fields with `[ProtoField]`
- **Auto-mapping** — public properties are included by default with auto-assigned, collision-free field numbers
- **Complex types** — arrays, `List<T>`, `Dictionary<K,V>`, nullable value types, enums, nested messages, inheritance
- **Advanced attributes** — `[ProtoMap]` for dictionaries, `[ProtoOneOf]` for unions, `[ProtoInclude]` for polymorphism, `[ProtoService]`/`[ProtoMethod]` for gRPC
- **Flexible Attributes** — Shorthand constructors like `[ProtoField(1)]`, `[ProtoContract("Name")]`, and support for enums
- **Versioning & Metadata** — `Version` and `Metadata` properties on contracts for schema organisation and documentation
- **Packed encoding** — scalar collections use proto3 packed wire format
- **Streaming** — length-delimited framing for multi-message streams
- **Bi-directional** — `ProtobufDuplexStream<TSend, TReceive>` for full-duplex communication
- **Validation** — `ValidationPipeline<T>` with configurable rules on send/receive
- **Async** — full `async`/`await` and `IAsyncEnumerable<T>` support
- **Static messages** — pre-compiled encode/decode delegates to eliminate reflection overhead
- **Schema generation** — auto-generate versioned `.proto` files with cross-file imports, service definitions, and request/response wrappers
- **Schema decoding** — decode protobuf binary using only `.proto` schemas, no C# types needed
- **ASP.NET Core** — input/output formatters and `HttpClient` extensions
- **WebSockets** — managed connections, broadcast, lifecycle hooks, and auto-reconnect
- **gRPC** — code-first services with typed client proxies and simplified DI registration
- **Unified setup** — single `AddProtobuffEncoder()` call with strategy pattern for all transports
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

### ASP.NET Core Setup (All Transports)

```csharp
using ProtobuffEncoder.AspNetCore.Setup;

builder.Services.AddProtobuffEncoder(options =>
{
    options.EnableMvcFormatters = true;
})
.WithRestFormatters()
.WithWebSocket(ws => ws
    .AddEndpoint<NotificationMessage, NotificationMessage>())
.WithGrpc(grpc => grpc
    .UseKestrel(httpPort: 5400, grpcPort: 5401)
    .AddService<WeatherGrpcServiceImpl>());

var app = builder.Build();
app.UseWebSockets();
app.MapProtobufEndpoints();
app.Run();
```

---

## Documentation

### Getting Started

| Guide | What you'll learn |
|-------|-------------------|
| [Setup & Configuration](guides/setup.md) | Unified `AddProtobuffEncoder()` builder, options pattern, strategy pattern, custom transports |
| [Attributes](guides/attributes.md) | `[ProtoContract]`, `[ProtoField]`, `[ProtoIgnore]`, `[ProtoMap]`, `[ProtoOneOf]`, `[ProtoInclude]`, `[ProtoService]`, `[ProtoMethod]` |
| [Serialization](guides/serialization.md) | Type mapping, field numbering, collections, nullable types, streaming, static messages |

### Transport & Communication

| Guide | What you'll learn |
|-------|-------------------|
| [Transport](guides/transport.md) | `ProtobufSender`, `ProtobufReceiver`, `ProtobufDuplexStream`, validation pipelines |
| [WebSockets](guides/websockets.md) | `MapProtobufWebSocket`, connection management, broadcast, `ProtobufWebSocketClient`, retry policy |
| [gRPC](guides/grpc.md) | `[ProtoService]`/`[ProtoMethod]`, server binding, `CreateProtobufClient<T>()`, duplex streaming |

### Reference

| Guide | What you'll learn |
|-------|-------------------|
| [ASP.NET Core](api/aspnetcore.md) | MVC formatters, `HttpClient` extensions, `ProtobufHttpContent` |
| [Schema](guides/schema.md) | Proto schema generation, parsing, schema-based decoding, `ProtobufWriter` |
| [CLI Tool](api/tool.md) | `ProtobuffEncoder.Tool` usage, MSBuild integration |
| [Demos](demos/README.md) | Running all 7 demo applications with browser dashboards |

### Recommended Reading Order

If you're new to the library, follow this path:

1. **[Setup](guides/setup.md)** — register services and choose your transports
2. **[Attributes](guides/attributes.md)** — understand how C# types map to protobuf
3. **[Serialization](guides/serialization.md)** — encoding, decoding, type mapping, and streaming
4. **[Transport](guides/transport.md)** — sender, receiver, and duplex stream primitives
5. Pick your transport:
   - **[ASP.NET Core](api/aspnetcore.md)** for REST APIs
   - **[WebSockets](guides/websockets.md)** for real-time bidirectional communication
   - **[gRPC](guides/grpc.md)** for code-first gRPC services
6. **[Demos](demos/README.md)** — run the interactive demos to see everything in action

---

## Project Structure

```
ProtobuffEncoder/
├── src/
│   ├── ProtobuffEncoder/                        Core library
│   │   ├── Attributes/                          [ProtoContract], [ProtoField], [ProtoService], [ProtoMethod], ...
│   │   ├── Schema/                              Proto generation, parsing, decoding
│   │   ├── Transport/                           Sender, receiver, duplex, validation
│   │   └── build/                               MSBuild targets
│   ├── ProtobuffEncoder.AspNetCore/             REST formatters, HttpClient, unified setup
│   │   └── Setup/                               Options pattern, strategy pattern, builder
│   ├── ProtobuffEncoder.WebSockets/             WebSocket framework
│   ├── ProtobuffEncoder.Grpc/                   Code-first gRPC framework
│   │   ├── Server/                              IServiceMethodProvider, service binding
│   │   └── Client/                              DispatchProxy client factory
│   └── ProtobuffEncoder.Contracts/              Shared contracts + service interfaces
│       └── Services/                            IWeatherGrpcService, IChatGrpcService
│
├── tools/
│   └── ProtobuffEncoder.Tool/                   CLI for .proto generation
│
├── demos/
│   ├── Demo.Api.Sender/                         HTTP sender (port 5200)
│   ├── Demo.Api.Receiver/                       Schema-only receiver (port 5100)
│   ├── Demo.Bidirectional.Server/               WebSocket server (port 5300)
│   ├── Demo.Bidirectional.Client/               WebSocket console client
│   ├── Demo.Grpc.Server/                        gRPC server (port 5400)
│   ├── Demo.Grpc.Client/                        gRPC console client
│   ├── Demo.Console/                            Feature showcase
│   └── Demo.SchemaGen/                          Schema generation showcase
│
└── docs/
    ├── guides/                                  In-depth guides
    │   ├── setup.md                             Unified setup & configuration
    │   ├── attributes.md                        All attributes
    │   ├── serialization.md                     Encoding, decoding, type mapping
    │   ├── transport.md                         Stream primitives & validation
    │   ├── websockets.md                        WebSocket framework
    │   ├── grpc.md                              gRPC framework
    │   └── schema.md                            Schema generation & decoding
    ├── api/                                     API & tooling reference
    │   ├── aspnetcore.md                        ASP.NET Core integration
    │   └── tool.md                              CLI tool
    └── demos/                                   Demo documentation
        └── README.md                            Running the demos
```

## Performance & Benchmarking

The project include a dedicated benchmark project using `BenchmarkDotNet` to ensure high performance and zero-regression across .NET versions.

### Running Benchmarks
```powershell
dotnet run -c Release --project benchmarks/ProtobuffEncoder.Benchmarks/ProtobuffEncoder.Benchmarks.csproj
```

Typical performance on modern hardware (e.g., Core i9):
- **Small Message (Encode/Decode)**: ~150-300ns
- **Large Collections**: ~1-5μs depending on size

## Testing & Quality

Comprehensive test suite with **441+ tests** across 5 test projects using **FIRST-U Pass/Fail patterns**:

| Project | Tests | Coverage |
|---------|-------|----------|
| Core Library | 231 | Encode/decode, attributes, collections, maps, oneof, inheritance, validation, streaming, schema generation, cross-file imports, service wiring, concurrency |
| ASP.NET Core | 41 | Formatters, HttpClient extensions, setup builder, strategies (TestHost integration) |
| gRPC | 34 | Marshaller, service discovery (all 4 method types), channel/DI extensions |
| WebSockets | 123 | Stream, retry, connection manager, client lifecycle, endpoint integration |
| Tool | 12 | ProjectModifier, duplicate prevention, batch operations |

### Advanced Test Patterns
- **Rollback**: Recovery after failed decodes and stream errors
- **Deadlock-Resolution**: Concurrent encode/decode across types with timeout guards
- **Loading-Test**: Scaling message counts (1 → 1000) and payload sizes (100B → 100KB)
- **Resource-Stress-Test**: Memory pressure and rapid connect/disconnect
- **Bit-Error-Simulation**: Random bytes fuzzing, truncated messages, malformed varints
- **Component-Simulation**: Full pipeline tests with ASP.NET Core TestHost

### Benchmarks (7 categories)
Located in `benchmarks/ProtobuffEncoder.Benchmarks/`:
- Core encode/decode, collections, static vs. dynamic, streaming, validation, schema generation, payload scaling

See [test_strategy.md](guides/../test_strategy.md) for full details.

## Supported .NET Versions

| Package | net10.0 | net9.0 | net8.0 |
|---------|---------|--------|--------|
| ProtobuffEncoder | yes | yes | yes |
| ProtobuffEncoder.AspNetCore | yes | yes | yes |
| ProtobuffEncoder.WebSockets | yes | yes | yes |
| ProtobuffEncoder.Grpc | yes | yes | yes |
| ProtobuffEncoder.Contracts | yes | yes | yes |
| ProtobuffEncoder.Tool | yes | yes | yes |

Demo applications target net10.0.

## License

MIT
