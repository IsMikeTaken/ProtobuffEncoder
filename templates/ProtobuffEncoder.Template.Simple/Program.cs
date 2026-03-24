// ============================================================================
// ProtobuffEncoder — Simple Template
// ============================================================================
// This template covers the basics: defining contracts, encoding, decoding,
// and streaming messages over an in-memory stream.
//
// Run with:  dotnet run
// ============================================================================

using ProtobuffEncoder;
using ProtobuffEncoder.Attributes;

Console.WriteLine("=== ProtobuffEncoder — Simple Template ===\n");

// ── 1. Basic encode / decode ────────────────────────────────────────────────

var person = new Person
{
    Id = 1,
    Name = "Alice",
    Email = "alice@example.com",
    IsActive = true
};

byte[] bytes = ProtobufEncoder.Encode(person);
Console.WriteLine($"Encoded Person to {bytes.Length} bytes");

var decoded = ProtobufEncoder.Decode<Person>(bytes);
Console.WriteLine($"Decoded: Id={decoded.Id}, Name={decoded.Name}, Email={decoded.Email}, Active={decoded.IsActive}");

// ── 2. Nested messages ──────────────────────────────────────────────────────

var order = new Order
{
    OrderId = 42,
    Total = 99.95m,
    ShippingAddress = new Address
    {
        Street = "123 Main St",
        City = "London",
        PostCode = "SW1A 1AA"
    }
};

var orderBytes = ProtobufEncoder.Encode(order);
var decodedOrder = ProtobufEncoder.Decode<Order>(orderBytes);
Console.WriteLine($"\nOrder #{decodedOrder.OrderId}: £{decodedOrder.Total}, Ship to: {decodedOrder.ShippingAddress.City}");

// ── 3. Streaming (length-delimited) ─────────────────────────────────────────

Console.WriteLine("\n--- Streaming ---");

using var stream = new MemoryStream();

// Write three messages
for (int i = 1; i <= 3; i++)
{
    var msg = new Person { Id = i, Name = $"User-{i}", Email = $"user{i}@example.com" };
    ProtobufEncoder.WriteDelimitedMessage(msg, stream);
}

Console.WriteLine($"Wrote 3 delimited messages ({stream.Length} bytes total)");

// Read them back
stream.Position = 0;
foreach (var msg in ProtobufEncoder.ReadDelimitedMessages<Person>(stream))
{
    Console.WriteLine($"  Read: Id={msg.Id}, Name={msg.Name}");
}

// ── 4. Static (pre-compiled) encoder for repeated use ───────────────────────

Console.WriteLine("\n--- Static Encoder ---");

var staticMessage = ProtobufEncoder.CreateStaticMessage<Person>();
var fastBytes = staticMessage.Encode(person);
var fastDecoded = staticMessage.Decode(fastBytes);
Console.WriteLine($"Static roundtrip: {fastDecoded.Name} ({fastBytes.Length} bytes)");

Console.WriteLine("\nDone! Explore the models below to see how attributes work.");

// ============================================================================
// Models
// ============================================================================

[ProtoContract]
public class Person
{
    [ProtoField(1)]
    public int Id { get; set; }

    [ProtoField(2)]
    public string Name { get; set; } = "";

    [ProtoField(3)]
    public string Email { get; set; } = "";

    [ProtoField(4)]
    public bool IsActive { get; set; }
}

[ProtoContract]
public class Address
{
    [ProtoField(1)]
    public string Street { get; set; } = "";

    [ProtoField(2)]
    public string City { get; set; } = "";

    [ProtoField(3)]
    public string PostCode { get; set; } = "";
}

[ProtoContract]
public class Order
{
    [ProtoField(1)]
    public int OrderId { get; set; }

    [ProtoField(2)]
    public decimal Total { get; set; }

    [ProtoField(3)]
    public Address ShippingAddress { get; set; } = new();
}
