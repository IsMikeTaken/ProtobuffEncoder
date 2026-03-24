# Demo Projects

The solution includes several demo projects showcasing different transport patterns and integration scenarios.

## Solution Structure

```
demos/
├── console/
│   └── ProtobuffEncoder.Demo.Console          # Basic encode/decode/streaming
├── web/
│   ├── ProtobuffEncoder.Demo.Api.Sender       # REST API client (HttpClient)
│   ├── ProtobuffEncoder.Demo.Api.Receiver     # REST API server (ASP.NET Core)
│   ├── ProtobuffEncoder.Demo.Grpc.Server      # gRPC server
│   ├── ProtobuffEncoder.Demo.Grpc.Client      # gRPC client
│   ├── ProtobuffEncoder.Demo.Bidirectional.Server  # WebSocket server
│   ├── ProtobuffEncoder.Demo.Bidirectional.Client  # WebSocket client
│   └── ProtobuffEncoder.Demo.SchemaGen        # Schema generation demo
```

## Console Demo

Demonstrates core library features without networking:

- Encode and decode messages with all scalar types
- Collection and map serialization
- OneOf union encoding
- Length-delimited streaming over MemoryStream
- StaticMessage pre-compiled encode/decode
- Validation pipeline

## REST API Demo (Sender + Receiver)

**Receiver** (server):
- ASP.NET Core with protobuf MVC formatters
- Echo endpoint at `POST /api/echo`
- Accepts `application/x-protobuf` requests
- Returns protobuf-encoded responses

**Sender** (client):
- Uses `HttpClient.PostProtobufAsync<TReq, TRes>()` extension
- Sends protobuf-encoded weather requests
- Deserializes protobuf responses

## gRPC Demo (Server + Client)

**Server**:
- Hosts `WeatherGrpcServiceImpl` implementing `IWeatherGrpcService`
- Registered via `AddProtobufGrpcService<T>()`
- No `.proto` files -- code-first with `[ProtoService]`

**Client**:
- Creates typed client via `channel.CreateProtobufClient<IWeatherGrpcService>()`
- Demonstrates unary, server streaming, and duplex patterns

## Bidirectional WebSocket Demo (Server + Client)

**Server**:
- ASP.NET Core with `WithWebSocket()` builder
- Connection manager for broadcast
- Echo handler: receives message, broadcasts to all connected clients
- Lifecycle logging (connect, disconnect, error)

**Client**:
- `ProtobufWebSocketClient` with retry policy
- Request-response pattern
- Continuous listening mode
- Graceful disconnect

## Schema Generation Demo

- Loads `ProtobuffEncoder.Contracts` assembly
- Generates all `.proto` files to an output directory
- Demonstrates versioned output (`v1/Order.proto`)
- Shows cross-file imports
- Optionally patches a `.csproj`

## Running the Demos

### Console

```bash
dotnet run --project demos/ProtobuffEncoder.Demo.Console
```

### REST API

```bash
# Terminal 1: Start server
dotnet run --project demos/ProtobuffEncoder.Demo.Api.Receiver

# Terminal 2: Run client
dotnet run --project demos/ProtobuffEncoder.Demo.Api.Sender
```

### gRPC

```bash
# Terminal 1: Start server
dotnet run --project demos/ProtobuffEncoder.Demo.Grpc.Server

# Terminal 2: Run client
dotnet run --project demos/ProtobuffEncoder.Demo.Grpc.Client
```

### WebSocket

```bash
# Terminal 1: Start server
dotnet run --project demos/ProtobuffEncoder.Demo.Bidirectional.Server

# Terminal 2: Run client
dotnet run --project demos/ProtobuffEncoder.Demo.Bidirectional.Client
```

### Schema Generation

```bash
dotnet run --project demos/ProtobuffEncoder.Demo.SchemaGen
```

