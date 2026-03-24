// ──────────────────────────────────────────────────────────────
//  SIMPLE WEBSOCKET SETUP
//  Bidirectional protobuf messaging over WebSockets.
//  Register an endpoint type pair, map a path — done.
// ──────────────────────────────────────────────────────────────

using ProtobuffEncoder.WebSockets;
using ProtobuffEncoder.Demo.Setup.Shared;

var builder = WebApplication.CreateBuilder(args);

// 1. Register the WebSocket endpoint with its send/receive type pair.
//    This creates a WebSocketConnectionManager<ChatReply, ChatMessage> singleton
//    so you can broadcast to all connected clients later.
builder.Services.AddProtobufWebSocketEndpoint<ChatReply, ChatMessage>();

var app = builder.Build();
app.UseWebSockets();

// 2. Map the endpoint to a path with a message handler.
app.MapProtobufWebSocket<ChatReply, ChatMessage>("/ws/chat", options =>
{
    // Called when a new client connects.
    options.OnConnect = connection =>
    {
        Console.WriteLine($"[+] Client connected: {connection.ConnectionId}");
        return connection.SendAsync(new ChatReply
        {
            Text = "Welcome! Send a ChatMessage to start chatting.",
            IsSystem = true
        });
    };

    // Called for every message received from a client.
    options.OnMessage = (connection, message) =>
    {
        Console.WriteLine($"[{message.User}] {message.Text}");

        // Echo it back with a server prefix.
        return connection.SendAsync(new ChatReply
        {
            Text = $"Server received: \"{message.Text}\"",
            IsSystem = false
        });
    };

    // Called when a client disconnects.
    options.OnDisconnect = connection =>
    {
        Console.WriteLine($"[-] Client disconnected: {connection.ConnectionId}");
        return Task.CompletedTask;
    };
});

Console.WriteLine("Simple WebSocket demo listening on ws://localhost:5000/ws/chat");
app.Run();
