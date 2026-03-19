# ProtobuffEncoder

A lightweight, attribute-driven .NET library that serializes and deserializes C# objects to [Protocol Buffer](https://protobuf.dev/programming-guides/encoding/) binary wire format — no `.proto` files or code generation required.

## Features

- **Attribute-based** — mark classes with `[ProtoContract]` and optionally override fields with `[ProtoField]`
- **Auto-mapping** — public properties are included by default with auto-assigned, collision-free field numbers
- **Complex types** — arrays, `List<T>`, `Dictionary<K,V>`, nullable value types, enums, nested messages, inheritance
- **Advanced attributes** — `[ProtoMap]` for dictionaries, `[ProtoOneOf]` for unions, `[ProtoInclude]` for polymorphism, `[ProtoService]`/`[ProtoMethod]` for gRPC
- **Packed encoding** — scalar collections use proto3 packed wire format
- **Streaming** — length-delimited framing for multi-message streams
- **Bi-directional** — `ProtobufDuplexStream<TSend, TReceive>` for full-duplex communication
- **Validation** — `ValidationPipeline<T>` with configurable rules on send/receive
- **Async** — full `async`/`await` and `IAsyncEnumerable<T>` support
- **Static messages** — pre-compiled encode/decode delegates to eliminate reflection overhead
- **Schema generation** — auto-generate `.proto` files from C# types
- **Schema decoding** — decode protobuf binary using only `.proto` schemas, no C# types needed
- **ASP.NET Core** — input/output formatters and `HttpClient` extensions
- **WebSockets** — managed connections, broadcast, lifecycle hooks, and auto-reconnect
- **gRPC** — code-first services via `[ProtoService]`/`[ProtoMethod]` with typed client proxies
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
│   └── Demo.Console/                            Feature showcase
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
