# Demo Projects

The solution ships with a comprehensive set of demo applications organised into three categories: full integration demos, tiered setup guides, and standalone tools.

## Solution Structure

```
demos/
├── Setup/                                    # Tiered setup guides
│   ├── Shared/                               # Shared models and contracts
│   ├── Simple/
│   │   ├── Rest/Program.cs                   # Minimal API + Controller
│   │   ├── WebSockets/Program.cs             # Echo handler
│   │   └── Grpc/Program.cs                   # Code-first service
│   ├── Normal/
│   │   ├── Rest/Program.cs                   # Builder, options, HttpClient
│   │   ├── WebSockets/Program.cs             # Validation, multi-endpoint
│   │   └── Grpc/Program.cs                   # Kestrel ports, assembly scan
│   └── Advanced/
│       ├── Rest/Program.cs                   # Auto-discovery, ProtobufWriter, polymorphism
│       ├── WebSockets/Program.cs             # Sensor stream, broadcast, raw writer
│       └── Grpc/Program.cs                   # Schema generation, schema-only decode
│
├── ProtobuffEncoder.Demo.Console             # Core feature showcase
├── ProtobuffEncoder.Demo.Api.Sender          # REST client (HttpClient)
├── ProtobuffEncoder.Demo.Api.Receiver        # REST server (schema-only)
├── ProtobuffEncoder.Demo.Grpc.Server         # gRPC server
├── ProtobuffEncoder.Demo.Grpc.Client         # gRPC client
├── ProtobuffEncoder.Demo.Bidirectional.Server  # WebSocket server
├── ProtobuffEncoder.Demo.Bidirectional.Client  # WebSocket client
└── ProtobuffEncoder.Demo.SchemaGen           # Schema generation tool
```

---

## Setup Guides (New)

Nine standalone projects, one per transport per tier. Each has its own `Program.cs` you can read, run, and adapt. See the dedicated pages for full walkthroughs:

| Tier | REST | WebSockets | gRPC |
|------|------|------------|------|
| [Simple](simple_setup.md) | Formatters + Minimal API | Echo handler | `AddService<T>` |
| [Normal](normal_setup.md) | Builder, options, HttpClient | Validation pipeline | Assembly scan, Kestrel ports |
| [Advanced](advanced_setup.md) | ProtoRegistry, ProtobufWriter, polymorphism | Sensor stream, broadcast | Schema generation, schema-only decode |

### Running a Setup Demo

```bash
# Pick a tier and transport:
dotnet run --project demos/Setup/Simple/Rest
dotnet run --project demos/Setup/Normal/WebSockets
dotnet run --project demos/Setup/Advanced/Grpc
```

The Advanced demos print their resolver output to the console — field numbering strategies, generated `.proto` schemas, and registration status — so you can see exactly how the framework interprets each type.

---

## Integration Demos

Full-featured applications that demonstrate real-world usage patterns. Each web demo includes an interactive browser dashboard.

### Console Showcase

Demonstrates core library features without networking:

- Encode and decode messages with all scalar types
- Collection and map serialisation
- OneOf union encoding
- Length-delimited streaming over MemoryStream
- StaticMessage pre-compiled encode/decode
- Validation pipeline

```bash
dotnet run --project demos/ProtobuffEncoder.Demo.Console
dotnet run --project demos/ProtobuffEncoder.Demo.Console -- -v    # verbose
dotnet run --project demos/ProtobuffEncoder.Demo.Console -- -s    # silent
```

### REST API (Sender + Receiver)

| Demo | Port | Role |
|------|------|------|
| Demo.Api.Sender | 5200 | HttpClient caller with request builder dashboard |
| Demo.Api.Receiver | 5100 | Schema-only server with proto explorer dashboard |

The Receiver decodes protobuf using only `.proto` files — no compile-time reference to the Contracts assembly. The Sender uses `PostProtobufAsync` extensions to call the Receiver.

```bash
# Terminal 1
dotnet run --project demos/ProtobuffEncoder.Demo.Api.Receiver
# Terminal 2
dotnet run --project demos/ProtobuffEncoder.Demo.Api.Sender
```

### gRPC (Server + Client)

| Demo | Port | Role |
|------|------|------|
| Demo.Grpc.Server | 5400 / 5401 | Code-first gRPC with service dashboard |
| Demo.Grpc.Client | — | Interactive menu-driven client |

No `.proto` files. Services are defined with `[ProtoService]` and `[ProtoMethod]` attributes and discovered via assembly scanning.

```bash
# Terminal 1
dotnet run --project demos/ProtobuffEncoder.Demo.Grpc.Server
# Terminal 2
dotnet run --project demos/ProtobuffEncoder.Demo.Grpc.Client
```

### WebSocket Bidirectional (Server + Client)

| Demo | Port | Role |
|------|------|------|
| Demo.Bidirectional.Server | 5300 | Chat + weather stream with live dashboard |
| Demo.Bidirectional.Client | — | Interactive console client with retry policy |

The server hosts both protobuf binary endpoints and JSON bridge endpoints for the browser dashboard.

```bash
# Terminal 1
dotnet run --project demos/ProtobuffEncoder.Demo.Bidirectional.Server
# Terminal 2
dotnet run --project demos/ProtobuffEncoder.Demo.Bidirectional.Client
```

### Schema Generation

Loads the Contracts assembly, generates `.proto` files, and optionally patches a `.csproj` to include them as build items.

```bash
dotnet run --project demos/ProtobuffEncoder.Demo.SchemaGen
```
