using ProtobuffEncoder.Contracts.Models;
using ProtobuffEncoder.Contracts.Services;

namespace ProtobuffEncoder.Demo.Grpc.Client.Demos;

public class OrderProcessingDemo(IOrderProcessingService client) : IDemoStrategy
{
    public string DisplayName => "Orders  — Complex Graph & Duplex Streaming";

    public async Task ExecuteAsync()
    {
        Console.WriteLine("\n  --- Complex Order Domain Demo ---");
        Console.WriteLine("  Calling OrderProcessing/GetOrderAsync (Unary)...");

        var orderId = Guid.NewGuid();
        var order = await client.GetOrderAsync(new GetOrderRequest { OrderId = orderId });

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n  Received Order {order.Id} (Created {order.CreatedAt.LocalDateTime})");
        Console.ResetColor();
        
        Console.WriteLine($"  Customer: {order.Customer.FirstName} {order.Customer.LastName}");
        Console.WriteLine($"  Shipping: {order.Customer.Address.Street}, {order.Customer.Address.City} [{order.Customer.Address.Country}]");
        Console.WriteLine($"  Status:   {order.Status}");
        
        Console.WriteLine($"  Items ({order.Items.Count}):");
        foreach (var item in order.Items)
        {
            Console.WriteLine($"    - {item.Quantity}x {item.ProductName} @ {item.UnitPrice:C}");
        }

        Console.WriteLine("\n  --- Duplex Order Processing Stream ---");
        
        // Setup stream pipeline
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Timeout safety
        
        async IAsyncEnumerable<Order> GenerateOrders()
        {
            yield return new Order { Id = Guid.NewGuid(), Status = OrderStatus.PendingValidation };
            await Task.Delay(200);
            yield return new Order { Id = Guid.NewGuid(), Status = OrderStatus.Processing };
        }

        await foreach (var updated in client.ProcessOrdersAsync(GenerateOrders(), cts.Token))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [Store] Order {updated.Id} status changed to: {updated.Status}");
            Console.ResetColor();
        }

        Console.WriteLine("\n  Demo Complete.\n");
    }
}
