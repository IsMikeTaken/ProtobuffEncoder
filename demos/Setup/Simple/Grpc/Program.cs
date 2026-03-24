// ──────────────────────────────────────────────────────────────
//  SIMPLE gRPC SETUP
//  Code-first gRPC with no .proto files. Define a service
//  interface, implement it, register it — the framework handles
//  marshalling and endpoint discovery.
// ──────────────────────────────────────────────────────────────

using ProtobuffEncoder.AspNetCore;
using ProtobuffEncoder.AspNetCore.Setup;
using ProtobuffEncoder.Grpc;
using ProtobuffEncoder.Demo.Setup.Shared;

var builder = WebApplication.CreateBuilder(args);

// 1. Register the ProtobuffEncoder builder and attach gRPC.
//    AddService<T>() registers the service implementation for auto-mapping.
builder.Services.AddProtobuffEncoder()
    .WithGrpc(grpc => grpc.AddService<DemoGrpcServiceImpl>());

var app = builder.Build();

// 2. Map all registered gRPC endpoints in one call.
app.MapProtobufEndpoints();

Console.WriteLine("Simple gRPC demo listening on http://localhost:5000");
Console.WriteLine("Service: DemoService (Echo, PlaceOrder)");
app.Run();

// ─────────────────────────────────────────────────────────────
//  Service implementation — implements the IDemoGrpcService
//  contract defined in the Shared project.
// ─────────────────────────────────────────────────────────────

public class DemoGrpcServiceImpl : IDemoGrpcService
{
    public Task<DemoResponse> Echo(DemoRequest request)
    {
        return Task.FromResult(new DemoResponse
        {
            Message = $"gRPC Echo: {request.Name}, value={request.Value}"
        });
    }

    public Task<OrderConfirmation> PlaceOrder(OrderRequest request)
    {
        return Task.FromResult(new OrderConfirmation
        {
            OrderId = Guid.NewGuid().ToString("N")[..8],
            Total = request.Quantity * request.UnitPrice,
            Status = request.Quantity > 100 ? "Pending Approval" : "Confirmed"
        });
    }
}
