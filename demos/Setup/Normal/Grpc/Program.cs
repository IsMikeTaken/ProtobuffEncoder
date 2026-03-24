// ──────────────────────────────────────────────────────────────
//  NORMAL gRPC SETUP
//  Builds on the simple setup by adding:
//    • Kestrel port configuration (HTTP/1.1 + HTTP/2 on separate ports)
//    • Assembly-based service discovery (no manual AddService calls)
//    • Options for global behaviour control
// ──────────────────────────────────────────────────────────────

using ProtobuffEncoder.AspNetCore;
using ProtobuffEncoder.AspNetCore.Setup;
using ProtobuffEncoder.Grpc;
using ProtobuffEncoder.Transport;
using ProtobuffEncoder.Demo.Setup.Shared;

var builder = WebApplication.CreateBuilder(args);

// 1. Central options with the builder pattern.
builder.Services.AddProtobuffEncoder(options =>
{
    options.DefaultInvalidMessageBehavior = InvalidMessageBehavior.Throw;
})
.WithGrpc(grpc => grpc
    // Separate ports: 5000 for HTTP/1.1 (dashboard/health), 5001 for HTTP/2 (gRPC).
    .UseKestrel(httpPort: 5000, grpcPort: 5001)

    // Scan this assembly for every class that implements an [ProtoService] interface.
    // No manual registration needed — just add implementations and they appear.
    .AddServiceAssembly(typeof(Program).Assembly));

var app = builder.Build();

// 2. One call maps every discovered gRPC service.
app.MapProtobufEndpoints();

// A lightweight health endpoint on the HTTP/1.1 port.
app.MapGet("/health", () => Results.Ok(new { status = "ok", services = new[] { "DemoService" } }));

Console.WriteLine("Normal gRPC demo listening on:");
Console.WriteLine("  http://localhost:5000  — HTTP/1.1 (health check)");
Console.WriteLine("  http://localhost:5001  — HTTP/2 (gRPC)");
Console.WriteLine("  Services discovered: DemoService (Echo, PlaceOrder)");
app.Run();

// ─────────────────────────────────────────────────────────────
//  Service implementation — auto-discovered via AddServiceAssembly.
// ─────────────────────────────────────────────────────────────

public class DemoGrpcService : IDemoGrpcService
{
    public Task<DemoResponse> Echo(DemoRequest request)
    {
        Console.WriteLine($"[gRPC] Echo({request.Name}, {request.Value})");
        return Task.FromResult(new DemoResponse
        {
            Message = $"gRPC Echo: {request.Name}, value={request.Value}"
        });
    }

    public Task<OrderConfirmation> PlaceOrder(OrderRequest request)
    {
        Console.WriteLine($"[gRPC] PlaceOrder({request.ProductName}, qty={request.Quantity})");
        var total = request.Quantity * request.UnitPrice;
        return Task.FromResult(new OrderConfirmation
        {
            OrderId = Guid.NewGuid().ToString("N")[..8],
            Total = total,
            Status = total > 1000 ? "Pending Approval" : "Confirmed"
        });
    }
}
