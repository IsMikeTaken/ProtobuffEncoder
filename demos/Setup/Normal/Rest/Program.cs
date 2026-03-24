// ──────────────────────────────────────────────────────────────
//  NORMAL REST SETUP
//  Builds on the simple setup by adding:
//    • ProtobufEncoderOptions for global configuration
//    • The fluent builder pattern (AddProtobuffEncoder → WithRestFormatters)
//    • HttpClient extensions for calling other protobuf APIs
// ──────────────────────────────────────────────────────────────

using ProtobuffEncoder.AspNetCore;
using ProtobuffEncoder.AspNetCore.Setup;
using ProtobuffEncoder.Demo.Setup.Shared;
using ProtobuffEncoder.Transport;

var builder = WebApplication.CreateBuilder(args);

// 1. Use the builder pattern instead of bare AddProtobufFormatters().
//    This gives you central options and the ability to compose transports.
builder.Services.AddProtobuffEncoder(options =>
{
    // Enable MVC formatters so controllers auto-negotiate protobuf.
    options.EnableMvcFormatters = true;

    // Reject invalid messages instead of silently skipping them.
    options.DefaultInvalidMessageBehavior = InvalidMessageBehavior.Throw;

    // Optional: global hook for logging every validation failure.
    options.OnGlobalValidationFailure = (message, result) =>
        Console.WriteLine($"[Validation] {message.GetType().Name}: {result.ErrorMessage}");
})
.WithRestFormatters();

// 2. Register a named HttpClient for calling another protobuf service.
//    In production, this would point to your downstream microservice.
builder.Services.AddHttpClient("DownstreamApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5000");
});

builder.Services.AddControllers();

var app = builder.Build();

// ── Validated endpoint ───────────────────────────────────────

app.MapPost("/api/order", (OrderRequest order) =>
{
    // Manual validation — in the Normal tier you decide where checks go.
    if (string.IsNullOrWhiteSpace(order.ProductName))
        return Results.BadRequest("ProductName is required.");
    if (order.Quantity < 1)
        return Results.BadRequest("Quantity must be at least 1.");
    if (order.UnitPrice <= 0)
        return Results.BadRequest("UnitPrice must be positive.");

    return Results.Ok(new OrderConfirmation
    {
        OrderId = Guid.NewGuid().ToString("N")[..8],
        Total = order.Quantity * order.UnitPrice
    });
});

// ── Client-side demo endpoint ────────────────────────────────
// Shows how to use HttpClient extensions to call *this same server*.

app.MapGet("/api/round-trip", async (IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("DownstreamApi");

    // PostProtobufAsync sends application/x-protobuf and decodes the response.
    var confirmation = await client.PostProtobufAsync<OrderRequest, OrderConfirmation>(
        "/api/order",
        new OrderRequest { ProductName = "Widget", Quantity = 3, UnitPrice = 9.99 });

    return Results.Ok(new
    {
        roundTrip = true,
        confirmation.OrderId,
        confirmation.Total,
        confirmation.Status
    });
});

app.MapControllers();

Console.WriteLine("Normal REST demo on http://localhost:5000");
Console.WriteLine("  POST /api/order         — validated order endpoint");
Console.WriteLine("  GET  /api/round-trip     — HttpClient round-trip demo");
app.Run();
