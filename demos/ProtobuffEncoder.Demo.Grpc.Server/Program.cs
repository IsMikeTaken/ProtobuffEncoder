using ProtobuffEncoder.AspNetCore.Setup;
using ProtobuffEncoder.Demo.Grpc.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Unified setup — two ports: HTTP/1.1 on 5400 (browser dashboard) and HTTP/2 on 5401 (gRPC).
// Kestrel cannot negotiate HTTP/2 over cleartext, so separate endpoints are required.
builder.Services.AddProtobuffEncoder()
    .WithGrpc(grpc => grpc
        .UseKestrel(httpPort: 5400, grpcPort: 5401)
        .AddService<WeatherGrpcServiceImpl>()
        .AddService<ChatGrpcServiceImpl>());

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Maps all auto-mapped gRPC service endpoints
app.MapProtobufEndpoints();

app.MapGet("/health", () => Results.Ok(new
{
    status = "gRPC server is running",
    services = new[] { "Weather (Unary, ServerStreaming)", "Chat (DuplexStreaming, Unary)" },
    transport = "ProtobuffEncoder (no .proto files)",
    endpoints = new { http = "http://localhost:5400 (dashboard)", grpc = "http://localhost:5401 (gRPC)" }
}));

app.Run();
