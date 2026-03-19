# Demo Applications

The solution includes several demo applications that showcase different features of the ProtobuffEncoder library. Each web demo includes an interactive browser dashboard.

## Overview

| Demo | Type | Port | Description |
|------|------|------|-------------|
| Demo.Api.Sender | Web API | 5200 | HTTP sender with interactive request builder |
| Demo.Api.Receiver | Web API | 5100 | Schema-only receiver with proto schema explorer |
| Demo.Bidirectional.Server | Web API | 5300 | WebSocket server with real-time streaming dashboard |
| Demo.Bidirectional.Client | Console | - | WebSocket client for bidirectional streaming |
| Demo.Grpc.Server | Web API | 5400 / 5401 | Code-first gRPC server (no .proto files) |
| Demo.Grpc.Client | Console | - | gRPC client with typed proxy (connects to 5401) |
| Demo.Console | Console | - | Feature showcase (encoding, streaming, validation) |

## HTTP Sender & Receiver

Demonstrates API-to-API protobuf communication where the Sender encodes C# objects to protobuf and the Receiver decodes using only `.proto` schemas.

### Running

```bash
# Start the Receiver first (port 5100)
dotnet run --project demos/ProtobuffEncoder.Demo.Api.Receiver

# Start the Sender (port 5200)
dotnet run --project demos/ProtobuffEncoder.Demo.Api.Sender
```

### Browser Dashboards

**Sender** — `http://localhost:5200`
- Weather request form (city, days, hourly wind toggle)
- Notification form (source, text, level, tags)
- Visual message flow diagram showing each step of the protobuf round-trip
- Results panel with timing and response data

**Receiver** — `http://localhost:5100`
- Schema explorer sidebar listing all loaded messages and enums
- Click any message to see its field table (number, name, type, label)
- Click any enum to see its values and numbers
- Overview page with syntax-highlighted `.proto` source

### API Endpoints

**Sender** (`http://localhost:5200`):
- `GET /api/send-weather?city=Amsterdam&days=3&includeHourly=true` — sends a weather request to the Receiver via protobuf, returns JSON
- `POST /api/send-notification` — forwards a JSON notification to the Receiver as protobuf
- `GET /health` — health check

**Receiver** (`http://localhost:5100`):
- `POST /api/weather` — accepts protobuf `WeatherRequest`, returns protobuf `WeatherResponse`
- `POST /api/notifications` — accepts protobuf `NotificationMessage`, returns protobuf `AckResponse`
- `GET /api/schema` — lists registered messages and enums
- `GET /api/schema/{name}` — message field table or enum values
- `GET /api/proto-source` — raw `.proto` file content
- `GET /health` — health check

### Testing with curl

```bash
# Weather request via Sender
curl "http://localhost:5200/api/send-weather?city=Amsterdam&days=3"

# Notification via Sender
curl -X POST http://localhost:5200/api/send-notification \
  -H "Content-Type: application/json" \
  -d '{"source":"CLI","text":"CPU high","level":"Warning","tags":["infra"]}'
```

## Bidirectional Streaming

Demonstrates WebSocket-based bidirectional protobuf streaming using `ProtobufDuplexStream`.

### Running

```bash
# Start the WebSocket server (port 5300)
dotnet run --project demos/ProtobuffEncoder.Demo.Bidirectional.Server

# Run the console client
dotnet run --project demos/ProtobuffEncoder.Demo.Bidirectional.Client
```

### Browser Dashboard

Open `http://localhost:5300` for an interactive dashboard with:

**Chat Panel**
- Connect/disconnect to the WebSocket chat endpoint
- Send messages with configurable level (Info/Warning/Error)
- Real-time message display with color-coded levels
- Protobuf byte size shown on each message
- Server validates messages (rejects empty text)
- Milestone notifications every 3 messages

**Weather Panel**
- Connect/disconnect to the weather stream endpoint
- Request forecasts by city, days, and wind toggle
- Weather cards show temperature, condition, humidity
- Protobuf request/response byte sizes displayed

### WebSocket Endpoints

- `ws://localhost:5300/ws/chat` — bidirectional chat using protobuf binary (for native clients)
- `ws://localhost:5300/ws/weather-stream` — weather request/response using protobuf binary
- `ws://localhost:5300/ws/chat/json` — JSON bridge for the browser dashboard
- `ws://localhost:5300/ws/weather-stream/json` — JSON bridge for the browser dashboard

The JSON endpoints encode/decode through the protobuf library on every message to demonstrate the round-trip.

### Console Client

The console client connects via WebSocket and runs two demos:

1. **Chat** — sends 5 messages concurrently with receiving, including an empty message to trigger server-side validation rejection
2. **Weather** — sequential request-response for Amsterdam, London, and Tokyo

## gRPC

Demonstrates code-first gRPC using `[ProtoService]` and `[ProtoMethod]` attributes with the
unified `AddProtobuffEncoder()` setup. No `.proto` files or code generation required.

### Running

```bash
# Start the gRPC server (HTTP/1.1 on 5400, HTTP/2 on 5401)
dotnet run --project demos/ProtobuffEncoder.Demo.Grpc.Server

# Run the console client (connects to gRPC on 5401)
dotnet run --project demos/ProtobuffEncoder.Demo.Grpc.Client
```

### Browser Dashboard

Open `http://localhost:5400` (HTTP/1.1 port) for an overview dashboard showing:
- Registered services and methods
- Method types (Unary, ServerStreaming, DuplexStreaming)
- Request/response type mappings
- gRPC route table
- Quick-start code examples

### Services

**Weather** (`/Weather/...`):
- `GetForecast` (Unary) — single city forecast
- `StreamForecasts` (Server Streaming) — day-by-day forecast stream with simulated delays

**Chat** (`/Chat/...`):
- `Chat` (Duplex Streaming) — bidirectional message stream with command routing (`/ping`, `/time`, `/stats`)
- `SendNotification` (Unary) — single notification with acknowledgement

### Console Client

Interactive menu with four demos:
1. **Unary Weather** — configure city, days, and wind; receive a full forecast
2. **Streaming Weather** — watch forecasts arrive day-by-day in real time
3. **Unary Notification** — send a message and receive an `AckResponse`
4. **Duplex Chat** — configurable message count and delay, concurrent send/receive

## Console Showcase

Demonstrates all core library features in a single console application.

### Running

```bash
dotnet run --project demos/ProtobuffEncoder.Demo.Console
dotnet run --project demos/ProtobuffEncoder.Demo.Console -- -v    # verbose (trace spans)
dotnet run --project demos/ProtobuffEncoder.Demo.Console -- -s    # silent
```

### Showcases

1. **Basic Encode/Decode** — round-trip encoding of a complex `Person` object with nested types, collections, enums, and nullable fields. Also demonstrates static message compilation.

2. **Async Streaming** — writes multiple messages to a stream with length-delimited framing, then reads them back as an `IAsyncEnumerable<T>`.

3. **Bi-Directional Streaming** — creates a `ProtobufDuplexStream` over memory streams, sends client requests and receives server responses concurrently.

4. **Validated Transport** — uses `ValidatedProtobufSender` with predicate rules to demonstrate send-time validation. Sends a valid message, then catches the `MessageValidationException` from an invalid one.

Each showcase reports byte sizes, timing, and trace spans (when verbose).
