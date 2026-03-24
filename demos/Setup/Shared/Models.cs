using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Demo.Setup.Shared;

// ──────────────────────────────────────────────────────────────
//  Shared contracts used across all setup demos (Simple, Normal, Advanced).
//  Every model uses [ProtoContract] + [ProtoField] so it works out of the box.
// ──────────────────────────────────────────────────────────────

[ProtoContract]
public class DemoRequest
{
    [ProtoField(1)] public string Name { get; set; } = "";
    [ProtoField(2)] public int Value { get; set; }
}

[ProtoContract]
public class DemoResponse
{
    [ProtoField(1)] public string Message { get; set; } = "";
    [ProtoField(2)] public long TimestampUtc { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

[ProtoContract]
public class ChatMessage
{
    [ProtoField(1)] public string User { get; set; } = "";
    [ProtoField(2)] public string Text { get; set; } = "";
    [ProtoField(3)] public long SentAtUtc { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

[ProtoContract]
public class ChatReply
{
    [ProtoField(1)] public string Text { get; set; } = "";
    [ProtoField(2)] public bool IsSystem { get; set; }
    [ProtoField(3)] public long SentAtUtc { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

[ProtoContract]
public class OrderRequest
{
    [ProtoField(1)] public string ProductName { get; set; } = "";
    [ProtoField(2)] public int Quantity { get; set; }
    [ProtoField(3)] public double UnitPrice { get; set; }
}

[ProtoContract]
public class OrderConfirmation
{
    [ProtoField(1)] public string OrderId { get; set; } = "";
    [ProtoField(2)] public double Total { get; set; }
    [ProtoField(3)] public string Status { get; set; } = "Confirmed";
}

// ──────────────────────────────────────────────────────────────
//  gRPC service contract
// ──────────────────────────────────────────────────────────────

[ProtoService("DemoService")]
public interface IDemoGrpcService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<DemoResponse> Echo(DemoRequest request);

    [ProtoMethod(ProtoMethodType.Unary)]
    Task<OrderConfirmation> PlaceOrder(OrderRequest request);
}
