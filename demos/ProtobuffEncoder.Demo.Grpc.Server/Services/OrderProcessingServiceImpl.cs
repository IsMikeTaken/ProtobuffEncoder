using System.Runtime.CompilerServices;
using ProtobuffEncoder.Contracts.Models;
using ProtobuffEncoder.Contracts.Services;

namespace ProtobuffEncoder.Demo.Grpc.Server.Services;

public class OrderProcessingServiceImpl : IOrderProcessingService
{
    public Task<Order> GetOrderAsync(GetOrderRequest request)
    {
        var order = new Order
        {
            Id = request.OrderId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            Status = OrderStatus.Shipped,
            Customer = new CustomerDetails
            {
                FirstName = "Jane",
                LastName = "Doe",
                Address = new ShippingAddress
                {
                    Street = "123 Protobuf Lane",
                    City = "Byteville",
                    PostalCode = "10101",
                    Country = "Siliconland"
                }
            },
            Items =
            [
                new OrderLineItem { ProductId = "P100", ProductName = "Mechanical Keyboard", Quantity = 1, UnitPrice = 149.99m },
                new OrderLineItem { ProductId = "M200", ProductName = "Wireless Mouse", Quantity = 2, UnitPrice = 49.50m }
            ]
        };

        return Task.FromResult(order);
    }

    public async IAsyncEnumerable<Order> ProcessOrdersAsync(
        IAsyncEnumerable<Order> ordersStream, 
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var order in ordersStream.WithCancellation(ct))
        {
            // Simulate processing
            order.Status = OrderStatus.Processing;
            yield return order;
            
            // Advance status
            order.Status = OrderStatus.Shipped;
            yield return order;
        }
    }
}
