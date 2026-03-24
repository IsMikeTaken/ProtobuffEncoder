// ──────────────────────────────────────────────────────────────
//  ADVANCED WEBSOCKET SETUP
//  Demonstrates features for maximum control:
//    • Auto-discovered types over WebSockets
//    • Validation pipeline with custom validators
//    • Connection manager for broadcast patterns
//    • Schema generation for connected message types
//    • ProtobufWriter for raw message construction
//
//  Run this demo and observe the console — it prints the
//  resolver output and shows the schema for each message type.
// ──────────────────────────────────────────────────────────────

using ProtobuffEncoder;
using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Schema;
using ProtobuffEncoder.Transport;
using ProtobuffEncoder.WebSockets;
using ProtobuffEncoder.Demo.Setup.Shared;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────
//  1. AUTO-DISCOVERY — register message types for WebSocket use.
// ─────────────────────────────────────────────────────────────

ProtoRegistry.Configure(opts =>
{
    opts.AutoDiscover = true;
    opts.DefaultFieldNumbering = FieldNumbering.Alphabetical;
});

// Register the endpoint type pairs.
builder.Services.AddProtobufWebSocketEndpoint<SensorReading, SensorCommand>();
builder.Services.AddProtobufWebSocketEndpoint<ChatReply, ChatMessage>();

var app = builder.Build();
app.UseWebSockets();

// ─────────────────────────────────────────────────────────────
//  2. RESOLVER OUTPUT — print schemas for WebSocket types.
// ─────────────────────────────────────────────────────────────

Console.WriteLine("╔══════════════════════════════════════════════════╗");
Console.WriteLine("║      ADVANCED WEBSOCKET — RESOLVER OUTPUT       ║");
Console.WriteLine("╚══════════════════════════════════════════════════╝");
Console.WriteLine();

PrintSchema<SensorReading>("SensorReading (auto-discovered, Alphabetical)");
PrintSchema<SensorCommand>("SensorCommand (auto-discovered, Alphabetical)");

// Show that attributed types still work alongside auto-discovered ones.
PrintSchema<ChatReply>("ChatReply (attributed — [ProtoContract])");
PrintSchema<ChatMessage>("ChatMessage (attributed — [ProtoContract])");

// ─────────────────────────────────────────────────────────────
//  3. SENSOR ENDPOINT — validation + broadcast.
// ─────────────────────────────────────────────────────────────

app.MapProtobufWebSocket<SensorReading, SensorCommand>("/ws/sensors", options =>
{
    // Validate incoming commands.
    options.ConfigureReceiveValidation = pipeline =>
    {
        pipeline.Require(
            cmd => !string.IsNullOrWhiteSpace(cmd.SensorId),
            "SensorId is required.");
        pipeline.Require(
            cmd => cmd.IntervalMs >= 100,
            "IntervalMs must be at least 100.");
    };

    options.OnInvalidReceive = InvalidMessageBehavior.Skip;

    options.OnMessageRejected = (conn, cmd, result) =>
    {
        Console.WriteLine($"[Sensor] Rejected from {conn.ConnectionId}: {result.ErrorMessage}");
        return conn.SendAsync(new SensorReading
        {
            SensorId = "SYSTEM",
            Value = 0,
            Unit = $"Error: {result.ErrorMessage}"
        });
    };

    options.OnConnect = conn =>
    {
        Console.WriteLine($"[Sensor] Client {conn.ConnectionId} connected");
        return conn.SendAsync(new SensorReading
        {
            SensorId = "SYSTEM",
            Value = 0,
            Unit = "Connected. Send a SensorCommand to start."
        });
    };

    options.OnMessage = async (conn, command) =>
    {
        Console.WriteLine($"[Sensor] {conn.ConnectionId} requested {command.SensorId} @ {command.IntervalMs}ms");

        // Simulate a few sensor readings.
        var random = new Random();
        for (var i = 0; i < 3; i++)
        {
            await conn.SendAsync(new SensorReading
            {
                SensorId = command.SensorId,
                Value = Math.Round(random.NextDouble() * 100, 2),
                Unit = "°C"
            });
            await Task.Delay(command.IntervalMs);
        }
    };

    options.OnDisconnect = conn =>
    {
        Console.WriteLine($"[Sensor] Client {conn.ConnectionId} disconnected");
        return Task.CompletedTask;
    };
});

// ── Chat endpoint with broadcast ─────────────────────────────

app.MapProtobufWebSocket<ChatReply, ChatMessage>("/ws/chat", options =>
{
    options.OnConnect = conn =>
    {
        Console.WriteLine($"[Chat] {conn.ConnectionId} joined");
        return conn.SendAsync(new ChatReply { Text = "Welcome!", IsSystem = true });
    };

    options.OnMessage = async (conn, msg) =>
    {
        Console.WriteLine($"[Chat] {msg.User}: {msg.Text}");

        // Use the connection manager to broadcast to all connected clients.
        var manager = app.Services
            .GetRequiredService<WebSocketConnectionManager<ChatReply, ChatMessage>>();

        await manager.BroadcastAsync(new ChatReply
        {
            Text = $"{msg.User}: {msg.Text}",
            IsSystem = false
        });
    };
});

// ─────────────────────────────────────────────────────────────
//  4. PROTOBUF WRITER — raw message construction endpoint.
// ─────────────────────────────────────────────────────────────

Console.WriteLine("── ProtobufWriter demo ─────────────────────────");
var writer = new ProtobufWriter();
writer.WriteString(1, "temperature-01");
writer.WriteDouble(2, 23.7);
writer.WriteString(3, "°C");
var rawBytes = writer.ToByteArray();
Console.WriteLine($"  Manual SensorReading: {rawBytes.Length} bytes");
Console.WriteLine($"  Hex: {Convert.ToHexString(rawBytes)}");
Console.WriteLine();

Console.WriteLine("── WebSocket Endpoints ─────────────────────────");
Console.WriteLine("  ws://localhost:5000/ws/sensors  — validated sensor stream");
Console.WriteLine("  ws://localhost:5000/ws/chat     — broadcast chat");
Console.WriteLine();
app.Run();

// ─────────────────────────────────────────────────────────────
//  HELPERS
// ─────────────────────────────────────────────────────────────

static void PrintSchema<T>(string label)
{
    Console.WriteLine($"── {label} ──");
    try
    {
        Console.WriteLine(ProtoSchemaGenerator.Generate(typeof(T)));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  (could not generate: {ex.Message})");
    }
    Console.WriteLine();
}

// ─────────────────────────────────────────────────────────────
//  AUTO-DISCOVERED MODELS — no [ProtoContract] needed.
//  The resolver assigns field numbers alphabetically.
// ─────────────────────────────────────────────────────────────

// Expected resolver output (Alphabetical):
//   message SensorReading {
//     string SensorId = 1;   ← S
//     string Unit = 2;       ← U
//     double Value = 3;      ← V
//   }
public class SensorReading
{
    public string SensorId { get; set; } = "";
    public double Value { get; set; }
    public string Unit { get; set; } = "";
}

// Expected resolver output (Alphabetical):
//   message SensorCommand {
//     int32  IntervalMs = 1;  ← I
//     string SensorId = 2;    ← S
//   }
public class SensorCommand
{
    public string SensorId { get; set; } = "";
    public int IntervalMs { get; set; } = 1000;
}
