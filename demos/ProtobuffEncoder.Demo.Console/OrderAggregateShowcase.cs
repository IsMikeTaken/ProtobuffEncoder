using System.Diagnostics;
using ProtobuffEncoder.Contracts.Models;

public static class OrderAggregateShowcase
{
    public static async Task RunAsync(ActivitySource tracer, CancellationToken token, CliOptions options)
    {
        using var activity = tracer.StartActivity("OrderAggregateShowcase");
        Console.WriteLine("\n=== [SHOWCASE] Complex Order Aggregate ===");

        // 1. Arrange: Create a complex order with multiple levels
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Status = OrderStatus.Processing,
            Customer = new CustomerDetails
            {
                FirstName = "Jane",
                LastName = "Doe",
                Address = new ShippingAddress
                {
                    Street = "123 Protobuf Lane",
                    City = "DataCity",
                    PostalCode = "10101",
                    Country = "Binary Republic"
                }
            },
            Items =
            [
                new OrderLineItem { ProductId = "P1", ProductName = "High-speed Serializer", Quantity = 2, UnitPrice = 49.99m },
                new OrderLineItem { ProductId = "P2", ProductName = "Compact Varint Buffer", Quantity = 1, UnitPrice = 19.50m }
            ]
        };

        Console.WriteLine($"[1] Created complex order for {order.Customer.FirstName} {order.Customer.LastName}");
        Console.WriteLine($"    Order contains {order.Items.Count} items.");

        // 2. Act: Serialize
        byte[] bytes;
        using (var serializeActivity = tracer.StartActivity("SerializeOrder"))
        {
            bytes = ProtobuffEncoder.ProtobufEncoder.Encode(order);
            serializeActivity?.SetTag("payload_size", bytes.Length);
        }
        Console.WriteLine($"[2] Serialized complex order into {bytes.Length} bytes.");

        // 3. Act: Deserialize
        Order decodedOrder;
        using (var deserializeActivity = tracer.StartActivity("DeserializeOrder"))
        {
            decodedOrder = ProtobuffEncoder.ProtobufEncoder.Decode<Order>(bytes);
        }
        Console.WriteLine($"[3] Deserialized back to Order object.");

        // 4. Assert (Visual Check)
        Console.WriteLine($"    Decoded Status: {decodedOrder.Status}");
        Console.WriteLine($"    Decoded Address: {decodedOrder.Customer.Address.Street}, {decodedOrder.Customer.Address.City}");
        Console.WriteLine($"    Decoded Item 1: {decodedOrder.Items[0].ProductName} (x{decodedOrder.Items[0].Quantity})");

        // Verify values
        if (decodedOrder.Id == order.Id && 
            decodedOrder.Customer.FirstName == order.Customer.FirstName &&
            decodedOrder.Items.Count == order.Items.Count)
        {
            Console.WriteLine("[SUCCESS] Order aggregate round-trip successful!");
        }
        else
        {
            Console.WriteLine("[FAILURE] Order aggregate data mismatch!");
        }

        await Task.CompletedTask;
    }
}
