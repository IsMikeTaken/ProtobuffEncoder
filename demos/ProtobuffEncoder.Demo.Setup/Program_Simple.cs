using ProtobuffEncoder.Demo.Setup.Models;
using ProtobuffEncoder.AspNetCore;
using ProtobuffEncoder.WebSockets;
using ProtobuffEncoder.Grpc;

var builder = WebApplication.CreateBuilder(args);

// --- SIMPLE SETUP (Default) ---
// 1. Add REST Support (Controllers & Minimal APIs)
builder.Services.AddControllers()
    .AddProtobufFormatters();

// 2. Add WebSocket Support
builder.Services.AddProtobufWebSocketEndpoint<DemoResponse, DemoRequest>();

// 3. Add gRPC Support
builder.Services.AddGrpc();
builder.Services.AddProtobuffEncoder()
    .WithGrpc(grpc => grpc.AddService<DemoServiceImplementation>());

var app = builder.Build();

// --- ENDPOINT REGISTRATION ---

// REST: Minimal API
app.MapPost("/api/simple/echo", (DemoRequest request) => 
    new DemoResponse { Message = $"Echo: {request.Name}" });

// REST: Controllers
app.MapControllers();

// WebSockets
app.MapProtobufWebSocket<DemoResponse, DemoRequest>("/ws/simple");

// gRPC
app.MapGrpcService<DemoServiceImplementation>();

app.Run();

// Dummy implementation for the demo
public class DemoServiceImplementation : IDemoService
{
    public Task<DemoResponse> SayHello(DemoRequest request) => 
        Task.FromResult(new DemoResponse { Message = $"Hello {request.Name} from gRPC!" });
}
