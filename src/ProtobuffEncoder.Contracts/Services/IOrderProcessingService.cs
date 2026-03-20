using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Contracts.Models;

namespace ProtobuffEncoder.Contracts.Services;

/// <summary>
/// A complex gRPC service showcasing layered objects and varied RPC methods.
/// </summary>
[ProtoService("OrderProcessingService")]
public interface IOrderProcessingService
{
    /// <summary>
    /// Fetches an order by its ID. Showcases Unary requests with complex deeply-nested responses.
    /// </summary>
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<Order> GetOrderAsync(GetOrderRequest request);

    /// <summary>
    /// Processes a batch of new orders in a duplex stream, returning statuses in real-time.
    /// </summary>
    [ProtoMethod(ProtoMethodType.DuplexStreaming)]
    IAsyncEnumerable<Order> ProcessOrdersAsync(IAsyncEnumerable<Order> ordersStream, CancellationToken ct = default);
}
