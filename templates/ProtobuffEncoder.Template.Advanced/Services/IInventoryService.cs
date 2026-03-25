using ProtobuffEncoder.Attributes;

// --- Service (uses auto-discovered types as request/response) ---
[ProtoService("InventoryService")]
public interface IInventoryService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<StockLevel> CheckStock(InventoryQuery query);

    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    IAsyncEnumerable<StockLevel> WatchStock(InventoryQuery query, CancellationToken ct = default);
}