// ──────────────────────────────────────────────────────────────
//  ADVANCED gRPC SETUP
//  Demonstrates features for maximum control:
//    • Assembly scanning for automatic service discovery
//    • Auto-discovery of request/response types (no attributes)
//    • Schema generation showing the resolver output
//    • Multiple gRPC services in a single server
//    • Polymorphic types over gRPC
//
//  Run this demo and observe the console — it prints the
//  generated .proto schema for every discovered service and type.
// ──────────────────────────────────────────────────────────────

using System.Reflection;
using ProtobuffEncoder;
using ProtobuffEncoder.AspNetCore;
using ProtobuffEncoder.AspNetCore.Setup;
using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Grpc;
using ProtobuffEncoder.Schema;
using ProtobuffEncoder.Demo.Setup.Shared;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────
//  1. AUTO-DISCOVERY — register plain classes as gRPC messages.
// ─────────────────────────────────────────────────────────────

ProtoRegistry.Configure(opts =>
{
    opts.AutoDiscover = true;
    opts.DefaultFieldNumbering = FieldNumbering.Alphabetical;
});

// Explicitly register a type with a different strategy.
ProtoRegistry.Register<InventoryItem>(FieldNumbering.DeclarationOrder);

// ─────────────────────────────────────────────────────────────
//  2. BUILDER — assembly scanning discovers all services.
// ─────────────────────────────────────────────────────────────

builder.Services.AddProtobuffEncoder()
    .WithGrpc(grpc => grpc
        .UseKestrel(httpPort: 5000, grpcPort: 5001)
        .AddServiceAssembly(typeof(Program).Assembly));

var app = builder.Build();
app.MapProtobufEndpoints();

// ─────────────────────────────────────────────────────────────
//  3. RESOLVER OUTPUT — print schemas and registration state.
// ─────────────────────────────────────────────────────────────

Console.WriteLine("╔══════════════════════════════════════════════════╗");
Console.WriteLine("║         ADVANCED gRPC — RESOLVER OUTPUT         ║");
Console.WriteLine("╚══════════════════════════════════════════════════╝");
Console.WriteLine();

// Show registration state.
Console.WriteLine("── Registration status ─────────────────────────");
Console.WriteLine($"  InventoryItem   registered:  {ProtoRegistry.IsRegistered(typeof(InventoryItem))}");
Console.WriteLine($"  InventoryItem   numbering:   DeclarationOrder");
Console.WriteLine($"  StockLevel      registered:  {ProtoRegistry.IsRegistered(typeof(StockLevel))}  (auto-discover)");
Console.WriteLine($"  StockLevel      resolvable:  {ProtoRegistry.IsResolvable(typeof(StockLevel))}");
Console.WriteLine($"  Total registered types:      {ProtoRegistry.RegisteredTypes.Count}");
Console.WriteLine();

// Print individual schemas.
PrintSchema<InventoryItem>("InventoryItem (DeclarationOrder)");
PrintSchema<StockLevel>("StockLevel (auto-discovered, Alphabetical)");

// Print schemas for the attributed gRPC message types.
PrintSchema<DemoRequest>("DemoRequest (attributed)");
PrintSchema<DemoResponse>("DemoResponse (attributed)");
PrintSchema<OrderRequest>("OrderRequest (attributed)");
PrintSchema<OrderConfirmation>("OrderConfirmation (attributed)");

// Generate all schemas from the assembly at once.
Console.WriteLine("── Assembly-wide schema generation ─────────────");
var allSchemas = ProtoSchemaGenerator.GenerateAll(typeof(Program).Assembly);
Console.WriteLine($"  Generated {allSchemas.Count} .proto file(s):");
foreach (var kvp in allSchemas)
    Console.WriteLine($"    {kvp.Key}  ({kvp.Value.Length} chars)");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────
//  4. SCHEMA-ONLY DECODE — show how to decode without CLR types.
// ─────────────────────────────────────────────────────────────

Console.WriteLine("── Schema-only decode demo ─────────────────────");
var item = new InventoryItem { Sku = "WIDGET-01", Name = "Widget", Quantity = 42, UnitPrice = 9.99 };
var encoded = ProtobufEncoder.Encode(item);
Console.WriteLine($"  Encoded InventoryItem: {encoded.Length} bytes");

// Decode using only the schema — no CLR type reference needed.
var schema = ProtoSchemaGenerator.Generate(typeof(InventoryItem));
var decoder = SchemaDecoder.FromProtoContent(schema);
var decoded = decoder.Decode("InventoryItem", encoded);
Console.WriteLine($"  Decoded via schema: {decoded}");
Console.WriteLine();

// Health endpoint.
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    services = new[] { "DemoService", "InventoryService" },
    registeredTypes = ProtoRegistry.RegisteredTypes.Count
}));

Console.WriteLine("── gRPC Endpoints ──────────────────────────────");
Console.WriteLine("  http://localhost:5000  — HTTP/1.1 (health)");
Console.WriteLine("  http://localhost:5001  — HTTP/2 (gRPC)");
Console.WriteLine("  Services: DemoService, InventoryService");
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
//  AUTO-DISCOVERED MODELS — no attributes needed.
// ─────────────────────────────────────────────────────────────

// Expected resolver output (DeclarationOrder):
//   message InventoryItem {
//     string Sku = 1;         ← declared first
//     string Name = 2;
//     int32  Quantity = 3;
//     double UnitPrice = 4;   ← declared last
//   }
public class InventoryItem
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public double UnitPrice { get; set; }
}

// Expected resolver output (Alphabetical, auto-discovered):
//   message StockLevel {
//     bool   InStock = 1;     ← I
//     int32  Quantity = 2;    ← Q
//     string Sku = 3;         ← S
//     string Warehouse = 4;   ← W
//   }
public class StockLevel
{
    public string Sku { get; set; } = "";
    public string Warehouse { get; set; } = "";
    public int Quantity { get; set; }
    public bool InStock { get; set; }
}

// ─────────────────────────────────────────────────────────────
//  gRPC SERVICE — uses auto-discovered types.
// ─────────────────────────────────────────────────────────────

[ProtoService("InventoryService")]
public interface IInventoryGrpcService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<StockLevel> CheckStock(InventoryItem request);
}

public class InventoryGrpcServiceImpl : IInventoryGrpcService
{
    public Task<StockLevel> CheckStock(InventoryItem request)
    {
        Console.WriteLine($"[gRPC] CheckStock({request.Sku})");
        return Task.FromResult(new StockLevel
        {
            Sku = request.Sku,
            Warehouse = "EU-WEST-1",
            Quantity = request.Quantity > 0 ? request.Quantity : 100,
            InStock = true
        });
    }
}

// Re-use the shared IDemoGrpcService contract.
public class DemoGrpcServiceImpl : IDemoGrpcService
{
    public Task<DemoResponse> Echo(DemoRequest request) =>
        Task.FromResult(new DemoResponse { Message = $"gRPC Echo: {request.Name}" });

    public Task<OrderConfirmation> PlaceOrder(OrderRequest request) =>
        Task.FromResult(new OrderConfirmation
        {
            OrderId = Guid.NewGuid().ToString("N")[..8],
            Total = request.Quantity * request.UnitPrice
        });
}
