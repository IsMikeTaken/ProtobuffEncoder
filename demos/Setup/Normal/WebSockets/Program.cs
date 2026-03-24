// ──────────────────────────────────────────────────────────────
//  NORMAL WEBSOCKET SETUP
//  Builds on the simple setup by adding:
//    • Receive-side validation pipeline
//    • OnMessageRejected hook for invalid messages
//    • OnError hook for exception logging
//    • Multiple WebSocket endpoints on a single server
// ──────────────────────────────────────────────────────────────

using ProtobuffEncoder.WebSockets;
using ProtobuffEncoder.Transport;
using ProtobuffEncoder.Demo.Setup.Shared;

var builder = WebApplication.CreateBuilder(args);

// 1. Register both endpoint type pairs.
builder.Services.AddProtobufWebSocketEndpoint<ChatReply, ChatMessage>();
builder.Services.AddProtobufWebSocketEndpoint<DemoResponse, DemoRequest>();

var app = builder.Build();
app.UseWebSockets();

// ── Chat endpoint with validation ────────────────────────────

app.MapProtobufWebSocket<ChatReply, ChatMessage>("/ws/chat", options =>
{
    // Validate every incoming message before OnMessage fires.
    options.ConfigureReceiveValidation = pipeline =>
    {
        pipeline.Require(msg => !string.IsNullOrWhiteSpace(msg.User), "User is required.");
        pipeline.Require(msg => !string.IsNullOrWhiteSpace(msg.Text), "Text cannot be empty.");
        pipeline.Require(msg => msg.Text.Length <= 500, "Text must be 500 characters or fewer.");
    };

    // Skip invalid messages (don't close the connection).
    options.OnInvalidReceive = InvalidMessageBehavior.Skip;

    // Log rejected messages for diagnostics.
    options.OnMessageRejected = (connection, message, result) =>
    {
        Console.WriteLine($"[Rejected] {connection.ConnectionId}: {result.ErrorMessage}");
        return connection.SendAsync(new ChatReply
        {
            Text = $"Your message was rejected: {result.ErrorMessage}",
            IsSystem = true
        });
    };

    options.OnConnect = connection =>
    {
        Console.WriteLine($"[Chat] {connection.ConnectionId} joined");
        return connection.SendAsync(new ChatReply { Text = "Welcome to the chat!", IsSystem = true });
    };

    options.OnMessage = (connection, message) =>
    {
        Console.WriteLine($"[Chat] {message.User}: {message.Text}");
        return connection.SendAsync(new ChatReply { Text = $"{message.User} says: {message.Text}" });
    };

    options.OnError = (connection, ex) =>
    {
        Console.WriteLine($"[Error] {connection.ConnectionId}: {ex.Message}");
        return Task.CompletedTask;
    };

    options.OnDisconnect = connection =>
    {
        Console.WriteLine($"[Chat] {connection.ConnectionId} left");
        return Task.CompletedTask;
    };
});

// ── Echo endpoint (second WebSocket route) ───────────────────

app.MapProtobufWebSocket<DemoResponse, DemoRequest>("/ws/echo", options =>
{
    options.OnMessage = (connection, request) =>
        connection.SendAsync(new DemoResponse { Message = $"Echo: {request.Name}" });
});

Console.WriteLine("Normal WebSocket demo listening on:");
Console.WriteLine("  ws://localhost:5000/ws/chat   — validated chat");
Console.WriteLine("  ws://localhost:5000/ws/echo   — simple echo");
app.Run();
