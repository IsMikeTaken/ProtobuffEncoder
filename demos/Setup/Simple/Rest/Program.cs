// ──────────────────────────────────────────────────────────────
//  SIMPLE REST SETUP
//  The bare minimum to accept and return protobuf over HTTP.
//  Two lines of setup, then business as usual.
// ──────────────────────────────────────────────────────────────

using ProtobuffEncoder.AspNetCore;
using ProtobuffEncoder.Demo.Setup.Shared;

var builder = WebApplication.CreateBuilder(args);

// 1. Register MVC controllers and add protobuf formatters.
//    This single call enables application/x-protobuf content negotiation.
builder.Services.AddControllers()
    .AddProtobufFormatters();

var app = builder.Build();

// ── Minimal API endpoints ────────────────────────────────────

// POST /api/echo — receives a DemoRequest, echoes it back as a DemoResponse.
app.MapPost("/api/echo", (DemoRequest request) =>
    new DemoResponse { Message = $"Echo: {request.Name}, value={request.Value}" });

// POST /api/order — a slightly more realistic endpoint.
app.MapPost("/api/order", (OrderRequest order) =>
    new OrderConfirmation
    {
        OrderId = Guid.NewGuid().ToString("N")[..8],
        Total = order.Quantity * order.UnitPrice,
        Status = order.Quantity > 100 ? "Pending Approval" : "Confirmed"
    });

// GET /api/hello/{name} — returns a protobuf DemoResponse for GET requests too.
app.MapGet("/api/hello/{name}", (string name) =>
    new DemoResponse { Message = $"Hello, {name}!" });

// ── Controller endpoints (auto-discovered) ──────────────────
app.MapControllers();

Console.WriteLine("Simple REST demo on http://localhost:5000");
Console.WriteLine("  POST /api/echo            — Minimal API echo");
Console.WriteLine("  POST /api/order           — Minimal API order");
Console.WriteLine("  GET  /api/hello/{name}    — Minimal API greeting");
Console.WriteLine("  POST /api/demo/echo       — Controller echo");
Console.WriteLine("  POST /api/demo/order      — Controller order");
app.Run();
