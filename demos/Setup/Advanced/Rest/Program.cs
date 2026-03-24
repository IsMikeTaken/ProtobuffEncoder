// ──────────────────────────────────────────────────────────────
//  ADVANCED REST SETUP
//  Demonstrates features for maximum control:
//    • Auto-discovery via ProtoRegistry (no attributes needed)
//    • ProtobufWriter for manual, zero-reflection encoding
//    • ProtoSchemaGenerator to inspect resolver output
//    • SchemaDecoder for schema-only decoding (no CLR type)
//    • Polymorphism with [ProtoInclude]
//
//  Run this demo and observe the console output — it prints
//  exactly what the resolver produces for each registered type.
// ──────────────────────────────────────────────────────────────

using System.Reflection;
using ProtobuffEncoder;
using ProtobuffEncoder.AspNetCore;
using ProtobuffEncoder.AspNetCore.Setup;
using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Schema;
using ProtobuffEncoder.Demo.Setup.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProtobuffEncoder(options =>
{
    options.EnableMvcFormatters = true;
})
.WithRestFormatters();

var app = builder.Build();

// ─────────────────────────────────────────────────────────────
//  1. AUTO-DISCOVERY — register plain classes without attributes.
//     The resolver assigns field numbers automatically.
// ─────────────────────────────────────────────────────────────

// Register individual types with a specific numbering strategy.
ProtoRegistry.Register<Customer>(FieldNumbering.Alphabetical);
ProtoRegistry.Register<Invoice>(FieldNumbering.DeclarationOrder);

// Enable global auto-discovery: any class can be encoded without
// prior registration or [ProtoContract].
ProtoRegistry.Configure(opts =>
{
    opts.AutoDiscover = true;
    opts.DefaultFieldNumbering = FieldNumbering.TypeThenAlphabetical;
});

// ─────────────────────────────────────────────────────────────
//  2. RESOLVER OUTPUT — print what the resolver sees.
// ─────────────────────────────────────────────────────────────

Console.WriteLine("╔══════════════════════════════════════════════════╗");
Console.WriteLine("║          AUTO-DISCOVERY RESOLVER OUTPUT         ║");
Console.WriteLine("╚══════════════════════════════════════════════════╝");
Console.WriteLine();

PrintRegistrationStatus();
PrintSchemaFor<Customer>("Customer (Alphabetical)");
PrintSchemaFor<Invoice>("Invoice (DeclarationOrder)");
PrintSchemaFor<Product>("Product (Auto-discovered, TypeThenAlphabetical)");

// ─────────────────────────────────────────────────────────────
//  3. POLYMORPHISM — [ProtoInclude] inheritance hierarchy.
// ─────────────────────────────────────────────────────────────

PrintSchemaFor<Shape>("Shape (Polymorphic base)");

Console.WriteLine("── Polymorphism round-trip ──────────────────────");
Shape circle = new Circle { Name = "MyCircle", Radius = 5.0 };
var bytes = ProtobufEncoder.Encode(circle);
var decoded = ProtobufEncoder.Decode<Shape>(bytes);
Console.WriteLine($"  Encoded {circle.GetType().Name} → {bytes.Length} bytes");
Console.WriteLine($"  Decoded as {decoded.GetType().Name}: Name={decoded.Name}");
if (decoded is Circle c)
    Console.WriteLine($"  Radius={c.Radius}");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────
//  4. PROTOBUF WRITER — manual low-level encoding.
// ─────────────────────────────────────────────────────────────

app.MapPost("/api/manual", (HttpContext context) =>
{
    // Bypass all reflection — build the wire bytes by hand.
    var writer = new ProtobufWriter();
    writer.WriteString(1, "Manual Response");
    writer.WriteVarint(2, 42);
    writer.WriteDouble(3, 3.14159);
    writer.WriteBool(4, true);

    var rawBytes = writer.ToByteArray();
    context.Response.ContentType = "application/x-protobuf";
    return context.Response.Body.WriteAsync(rawBytes).AsTask();
});

// ─────────────────────────────────────────────────────────────
//  5. SCHEMA-ONLY DECODING — decode protobuf without CLR types.
// ─────────────────────────────────────────────────────────────

app.MapPost("/api/schema-decode", async (HttpContext context) =>
{
    // Generate a .proto schema from the Customer type.
    var protoSchema = ProtoSchemaGenerator.Generate(typeof(Customer));

    // Build a decoder from that schema string.
    var decoder = SchemaDecoder.FromProtoContent(protoSchema);

    // Read the raw body and decode it using the schema.
    using var ms = new MemoryStream();
    await context.Request.Body.CopyToAsync(ms);
    var message = decoder.Decode("Customer", ms.ToArray());

    // Return the decoded fields as JSON for inspection.
    return Results.Ok(new
    {
        decodedFrom = "schema (no CLR type)",
        fields = message.ToString()
    });
});

// ── Standard endpoints for auto-discovered types ─────────────

app.MapPost("/api/customer", (Customer customer) =>
    new DemoResponse { Message = $"Received customer: {customer.Name}, {customer.Email}" });

app.MapPost("/api/invoice", (Invoice invoice) =>
    new DemoResponse { Message = $"Invoice #{invoice.Number}: {invoice.LineItems.Count} items" });

Console.WriteLine("── REST Endpoints ──────────────────────────────");
Console.WriteLine("  POST /api/customer       — auto-discovered type");
Console.WriteLine("  POST /api/invoice        — auto-discovered type");
Console.WriteLine("  POST /api/manual         — ProtobufWriter output");
Console.WriteLine("  POST /api/schema-decode  — schema-only decoding");
Console.WriteLine();
app.Run();

// ─────────────────────────────────────────────────────────────
//  HELPER — prints the .proto schema the resolver generates.
// ─────────────────────────────────────────────────────────────

static void PrintRegistrationStatus()
{
    Console.WriteLine("── Registration status ─────────────────────────");
    Console.WriteLine($"  Customer  registered:  {ProtoRegistry.IsRegistered(typeof(Customer))}");
    Console.WriteLine($"  Invoice   registered:  {ProtoRegistry.IsRegistered(typeof(Invoice))}");
    Console.WriteLine($"  Product   registered:  {ProtoRegistry.IsRegistered(typeof(Product))}  (auto-discover)");
    Console.WriteLine($"  Product   resolvable:  {ProtoRegistry.IsResolvable(typeof(Product))}");
    Console.WriteLine($"  Total registered:      {ProtoRegistry.RegisteredTypes.Count}");
    Console.WriteLine();
}

static void PrintSchemaFor<T>(string label)
{
    Console.WriteLine($"── {label} ──");
    try
    {
        var schema = ProtoSchemaGenerator.Generate(typeof(T));
        Console.WriteLine(schema);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  (could not generate: {ex.Message})");
    }
    Console.WriteLine();
}

// ─────────────────────────────────────────────────────────────
//  MODELS — plain classes, no [ProtoContract] attributes.
//  The resolver assigns field numbers based on the strategy.
// ─────────────────────────────────────────────────────────────

// Expected schema output (Alphabetical numbering):
//   message Customer {
//     string Email = 1;      ← E comes first
//     bool   IsActive = 2;
//     string Name = 3;       ← N comes after I
//   }
public class Customer
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

// Expected schema output (DeclarationOrder numbering):
//   message Invoice {
//     string       Number = 1;     ← declared first
//     string       CustomerName = 2;
//     repeated ... LineItems = 3;
//     double       Total = 4;      ← declared last
//   }
public class Invoice
{
    public string Number { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public List<string> LineItems { get; set; } = [];
    public double Total { get; set; }
}

// Expected schema output (TypeThenAlphabetical — scalars, then collections):
//   message Product {
//     string       Category = 1;    ← scalar, C
//     string       Name = 2;        ← scalar, N
//     double       Price = 3;       ← scalar, P
//     repeated ... Tags = 4;        ← collection comes last
//   }
public class Product
{
    public string Name { get; set; } = "";
    public double Price { get; set; }
    public string Category { get; set; } = "";
    public List<string> Tags { get; set; } = [];
}

// ─────────────────────────────────────────────────────────────
//  POLYMORPHISM — [ProtoInclude] maps derived types to field numbers.
// ─────────────────────────────────────────────────────────────

// Expected schema output:
//   message Shape {
//     string Name = 1;
//     Circle circle = 10;
//     Rectangle rectangle = 11;
//   }
//   message Circle { double Radius = 1; }
//   message Rectangle { double Width = 1; double Height = 2; }

[ProtoContract]
[ProtoInclude(10, typeof(Circle))]
[ProtoInclude(11, typeof(Rectangle))]
public class Shape
{
    [ProtoField(1)] public string Name { get; set; } = "";
}

[ProtoContract]
public class Circle : Shape
{
    [ProtoField(1)] public double Radius { get; set; }
}

[ProtoContract]
public class Rectangle : Shape
{
    [ProtoField(1)] public double Width { get; set; }
    [ProtoField(2)] public double Height { get; set; }
}
