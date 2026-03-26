using ProtobuffEncoder.Attributes;
using ProtobuffEncoder.Template.Advanced.Contracts;

namespace ProtobuffEncoder.Template.Advanced.Services;

// Service interface with auto-discovered request/response types.
[ProtoService("InventoryService")]
public interface IInventoryService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<StockLevel> CheckStock(InventoryQuery query);

    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    IAsyncEnumerable<StockLevel> WatchStock(InventoryQuery query, CancellationToken ct = default);
}
