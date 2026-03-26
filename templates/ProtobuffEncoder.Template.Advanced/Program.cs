// ProtobuffEncoder — Advanced Template
//
// Auto-discovery, ProtoRegistry, field numbering strategies, assembly
// scanning, schema generation, and mixing attributed with plain types.
//
// Contracts live in Contracts/, the service interface in Services/.
//
// Run with: dotnet run

using System.Reflection;
using ProtobuffEncoder;
using ProtobuffEncoder.Schema;
using ProtobuffEncoder.Template.Advanced.Contracts;

Console.WriteLine("ProtobuffEncoder — Advanced Template\n");

// Auto-discovery — register a plain class, no attributes needed.

ProtoRegistry.Register<Customer>(FieldNumbering.Alphabetical);

var customer = new Customer
{
    Name = "Acme Ltd",
    Email = "info@acme.co.uk",
    CreditLimit = 50_000m
};

var customerBytes = ProtobufEncoder.Encode(customer);
var decodedCustomer = ProtobufEncoder.Decode<Customer>(customerBytes);
Console.WriteLine($"Customer: {decodedCustomer.Name}, credit=£{decodedCustomer.CreditLimit}");
Console.WriteLine($"  Registered: {ProtoRegistry.IsRegistered(typeof(Customer))}");

// Assembly scanning — register every public class in one call.

Console.WriteLine("\nAssembly scanning...");
ProtoRegistry.Reset();
int count = ProtoRegistry.RegisterAssembly(Assembly.GetExecutingAssembly());
Console.WriteLine($"  Registered {count} type(s) from this assembly");
Console.WriteLine($"  Customer:  {ProtoRegistry.IsRegistered(typeof(Customer))}");
Console.WriteLine($"  Invoice:   {ProtoRegistry.IsRegistered(typeof(Invoice))}");

// Global auto-discover — any class is serialisable without registration.

Console.WriteLine("\nGlobal auto-discover...");
ProtoRegistry.Reset();
ProtoRegistry.Configure(opts =>
{
    opts.AutoDiscover = true;
    opts.DefaultFieldNumbering = FieldNumbering.Alphabetical;
});

var invoice = new Invoice
{
    Number = "INV-2026-001",
    Amount = 1_250.00m,
    Currency = "GBP",
    DueDate = new DateTime(2026, 12, 31)
};

var invoiceBytes = ProtobufEncoder.Encode(invoice);
var decodedInvoice = ProtobufEncoder.Decode<Invoice>(invoiceBytes);
Console.WriteLine($"  Invoice {decodedInvoice.Number}: {decodedInvoice.Currency} {decodedInvoice.Amount}");
Console.WriteLine($"  Resolvable (auto): {ProtoRegistry.IsResolvable(typeof(Invoice))}");

// Field numbering strategies.

Console.WriteLine("\nField numbering strategies...");

var product = new Product { Name = "Widget", Price = 9.99m, Category = "Hardware" };

ProtoRegistry.Reset();
ProtoRegistry.Configure(opts => opts.DefaultFieldNumbering = FieldNumbering.DeclarationOrder);
ProtoRegistry.Register<Product>();
var b1 = ProtobufEncoder.Encode(product);
Console.WriteLine($"  DeclarationOrder:     {b1.Length} bytes  (Name=1, Price=2, Category=3)");

ProtoRegistry.Reset();
ProtoRegistry.Configure(opts => opts.DefaultFieldNumbering = FieldNumbering.Alphabetical);
ProtoRegistry.Register<Product>();
var b2 = ProtobufEncoder.Encode(product);
Console.WriteLine($"  Alphabetical:         {b2.Length} bytes  (Category=1, Name=2, Price=3)");

ProtoRegistry.Reset();
ProtoRegistry.Configure(opts => opts.DefaultFieldNumbering = FieldNumbering.TypeThenAlphabetical);
ProtoRegistry.Register<Product>();
var b3 = ProtobufEncoder.Encode(product);
Console.WriteLine($"  TypeThenAlphabetical: {b3.Length} bytes  (scalars first, then alphabetical)");

// Per-type overrides.

ProtoRegistry.Reset();
ProtoRegistry.Register<Product>(FieldNumbering.Alphabetical);
ProtoRegistry.Register<Customer>(FieldNumbering.DeclarationOrder);
Console.WriteLine("\n  Product  -> Alphabetical");
Console.WriteLine("  Customer -> DeclarationOrder");

// Mixed: attributed types always keep their explicit field numbers.

Console.WriteLine("\nMixed: attributed + auto-discovered...");
ProtoRegistry.Reset();
ProtoRegistry.Configure(opts => opts.AutoDiscover = true);

var attributed = new AttributedProduct { Sku = "SKU-001", Title = "Premium Widget", Weight = 1.5 };
var attrBytes = ProtobufEncoder.Encode(attributed);
var decodedAttr = ProtobufEncoder.Decode<AttributedProduct>(attrBytes);
Console.WriteLine($"  Attributed: {decodedAttr.Sku} — {decodedAttr.Title}, {decodedAttr.Weight} kg");

var plain = new Customer { Name = "Plain Co", Email = "plain@example.com", CreditLimit = 10_000m };
var plainBytes = ProtobufEncoder.Encode(plain);
var decodedPlain = ProtobufEncoder.Decode<Customer>(plainBytes);
Console.WriteLine($"  Auto-discovered: {decodedPlain.Name}");

// Schema generation.

Console.WriteLine("\nSchema generation...");
var schema = ProtoSchemaGenerator.Generate(typeof(AttributedProduct));
Console.WriteLine(schema);

Console.WriteLine("Assembly-wide schema generation...");
var allSchemas = ProtoSchemaGenerator.GenerateAll(Assembly.GetExecutingAssembly());
Console.WriteLine($"  Generated {allSchemas.Count} .proto file(s):");
foreach (var (key, content) in allSchemas)
    Console.WriteLine($"    {key} ({content.Length} chars)");

// The IInventoryService interface (in Services/) uses auto-discovered types.

Console.WriteLine("\nService: IInventoryService (see Services/IInventoryService.cs)");
Console.WriteLine("  CheckStock(InventoryQuery) -> StockLevel              [Unary]");
Console.WriteLine("  WatchStock(InventoryQuery) -> stream of StockLevel    [ServerStreaming]");

Console.WriteLine("\nDone.");
