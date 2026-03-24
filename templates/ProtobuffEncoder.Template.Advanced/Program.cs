// ============================================================================
// ProtobuffEncoder — Advanced Template
// ============================================================================
// This template covers advanced features: auto-discovery without attributes,
// the ProtoRegistry, field numbering strategies, assembly scanning, schema
// generation, and transport with ProtoValue/ProtoMessage streams.
//
// Run with:  dotnet run
// ============================================================================

using System.Reflection;
using ProtobuffEncoder;
using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Schema;

Console.WriteLine("=== ProtobuffEncoder — Advanced Template ===\n");

// ── 1. Auto-discovery: register plain classes without attributes ─────────────

Console.WriteLine("--- Auto-Discovery ---");

// Register a single type
ProtoRegistry.Register<Customer>();

// Now it can be encoded/decoded without [ProtoContract]
var customer = new Customer
{
    Name = "Acme Ltd",
    ContactEmail = "info@acme.co.uk",
    CreditLimit = 50_000m
};

var customerBytes = ProtobufEncoder.Encode(customer);
var decodedCustomer = ProtobufEncoder.Decode<Customer>(customerBytes);
Console.WriteLine($"Customer: {decodedCustomer.Name}, Credit: £{decodedCustomer.CreditLimit}");
Console.WriteLine($"  Registered types: {ProtoRegistry.RegisteredTypes.Count}");

// ── 2. Assembly scanning ─────────────────────────────────────────────────────

Console.WriteLine("\n--- Assembly Scanning ---");

ProtoRegistry.Reset(); // clean slate for demo purposes

int registered = ProtoRegistry.RegisterAssembly(Assembly.GetExecutingAssembly());
Console.WriteLine($"Scanned assembly: registered {registered} types");
Console.WriteLine($"  Customer registered? {ProtoRegistry.IsRegistered(typeof(Customer))}");
Console.WriteLine($"  Invoice registered?  {ProtoRegistry.IsRegistered(typeof(Invoice))}");

// ── 3. Global auto-discover mode ────────────────────────────────────────────

Console.WriteLine("\n--- Global Auto-Discover ---");

ProtoRegistry.Reset();
ProtoRegistry.Configure(opts =>
{
    opts.AutoDiscover = true;
    opts.DefaultFieldNumbering = FieldNumbering.Alphabetical;
});

// Any class can now be serialized — no registration or attributes needed
var invoice = new Invoice
{
    InvoiceNumber = "INV-2024-001",
    Amount = 1_250.00m,
    Currency = "GBP",
    DueDate = new DateTime(2024, 12, 31)
};

var invoiceBytes = ProtobufEncoder.Encode(invoice);
var decodedInvoice = ProtobufEncoder.Decode<Invoice>(invoiceBytes);
Console.WriteLine($"Invoice: {decodedInvoice.InvoiceNumber}, {decodedInvoice.Currency} {decodedInvoice.Amount}");

// ── 4. Field numbering strategies ───────────────────────────────────────────

Console.WriteLine("\n--- Field Numbering Strategies ---");

// DeclarationOrder: fields numbered in source order (1, 2, 3, ...)
ProtoRegistry.Reset();
ProtoRegistry.Configure(opts => opts.DefaultFieldNumbering = FieldNumbering.DeclarationOrder);
ProtoRegistry.Register<Product>();
var productBytes1 = ProtobufEncoder.Encode(new Product { Name = "Widget", Price = 9.99m, Category = "Hardware" });
Console.WriteLine($"DeclarationOrder: {productBytes1.Length} bytes");

// Alphabetical: fields numbered alphabetically by property name
ProtoRegistry.Reset();
ProtoRegistry.Configure(opts => opts.DefaultFieldNumbering = FieldNumbering.Alphabetical);
ProtoRegistry.Register<Product>();
var productBytes2 = ProtobufEncoder.Encode(new Product { Name = "Widget", Price = 9.99m, Category = "Hardware" });
Console.WriteLine($"Alphabetical:     {productBytes2.Length} bytes");

// TypeThenAlphabetical: scalars first, then collections, then nested messages
ProtoRegistry.Reset();
ProtoRegistry.Configure(opts => opts.DefaultFieldNumbering = FieldNumbering.TypeThenAlphabetical);
ProtoRegistry.Register<Product>();
var productBytes3 = ProtobufEncoder.Encode(new Product { Name = "Widget", Price = 9.99m, Category = "Hardware" });
Console.WriteLine($"TypeAlpha:        {productBytes3.Length} bytes");

// Per-type override
ProtoRegistry.Reset();
ProtoRegistry.Register<Product>(FieldNumbering.Alphabetical);
var b4 = ProtobufEncoder.Encode(new Product { Name = "W", Price = 1m, Category = "C" });
Console.WriteLine($"Per-type Alpha:   {b4.Length} bytes");

// ── 5. Mixing attributes with auto-discovery ────────────────────────────────

Console.WriteLine("\n--- Mixed: Attributes + Auto-Discovery ---");

ProtoRegistry.Reset();
ProtoRegistry.Configure(opts => opts.AutoDiscover = true);

// Attributed class uses explicit field numbers
var attributed = new AttributedProduct
{
    Sku = "SKU-001",
    Title = "Premium Widget",
    Weight = 1.5
};

var attrBytes = ProtobufEncoder.Encode(attributed);
var decodedAttr = ProtobufEncoder.Decode<AttributedProduct>(attrBytes);
Console.WriteLine($"Attributed: {decodedAttr.Sku} — {decodedAttr.Title}, {decodedAttr.Weight}kg");

// Non-attributed class auto-discovered
var plain = new Customer { Name = "Test Co", ContactEmail = "test@test.com", CreditLimit = 10_000m };
var plainBytes = ProtobufEncoder.Encode(plain);
var decodedPlain = ProtobufEncoder.Decode<Customer>(plainBytes);
Console.WriteLine($"Auto-discovered: {decodedPlain.Name}");

// ── 6. Schema generation ────────────────────────────────────────────────────

Console.WriteLine("\n--- Schema Generation ---");

var schema = ProtoSchemaGenerator.Generate(typeof(AttributedProduct));
Console.WriteLine($"Generated .proto schema:\n{schema}");

// ── 7. Transport: streaming ProtoMessage over a pipe ─────────────────────────

Console.WriteLine("--- ProtoMessage Transport ---");

using var pipe = new MemoryStream();

// Producer: write dynamic messages
for (int i = 1; i <= 5; i++)
{
    var msg = new ProtoMessage()
        .Set(1, $"Event-{i}")
        .Set(2, i * 100)
        .Set(3, DateTime.UtcNow);
    msg.WriteDelimitedTo(pipe);
}

Console.WriteLine($"Wrote 5 events to stream ({pipe.Length} bytes)");

// Consumer: read them back
pipe.Position = 0;
foreach (var msg in ProtoMessage.ReadAllDelimitedFrom(pipe))
{
    Console.WriteLine($"  {msg.GetString(1)}: value={msg.Get<int>(2)}");
}

// ── 8. Transport: streaming ProtoValue for simple values ─────────────────────

Console.WriteLine("\n--- ProtoValue Streaming ---");

using var valuePipe = new MemoryStream();

// Write individual typed values
var strings = new[] { "Hello", "World", "From ProtobuffEncoder \U0001F680" };
foreach (var s in strings)
{
    var encoded = ProtoValue.Encode(s);
    valuePipe.Write(BitConverter.GetBytes(encoded.Length));
    valuePipe.Write(encoded);
}

Console.WriteLine($"Wrote {strings.Length} string values ({valuePipe.Length} bytes)");

Console.WriteLine("\nDone! This template demonstrates the full range of advanced features.");

// ============================================================================
// Plain classes (no attributes — used with auto-discovery)
// ============================================================================

/// <summary>A plain class — no [ProtoContract] needed when registered or auto-discovered.</summary>
public class Customer
{
    public string Name { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public decimal CreditLimit { get; set; }
}

/// <summary>Another plain class for assembly scanning.</summary>
public class Invoice
{
    public string InvoiceNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime DueDate { get; set; }
}

/// <summary>A plain class showing field ordering behaviour.</summary>
public class Product
{
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string Category { get; set; } = "";
}

// ============================================================================
// Attributed class (uses explicit field numbers — always respected)
// ============================================================================

[ProtoContract(FieldNumbering = FieldNumbering.Alphabetical)]
public class AttributedProduct
{
    [ProtoField(10)]
    public string Sku { get; set; } = "";

    [ProtoField(20)]
    public string Title { get; set; } = "";

    [ProtoField(30)]
    public double Weight { get; set; }
}
