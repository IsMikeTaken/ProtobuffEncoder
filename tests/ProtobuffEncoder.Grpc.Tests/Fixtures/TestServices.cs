using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Grpc.Tests.Fixtures;

// ── Test contracts ──

[ProtoContract]
public class PingRequest
{
    [ProtoField(1)] public string Message { get; set; } = "";
}

[ProtoContract]
public class PingResponse
{
    [ProtoField(1)] public string Reply { get; set; } = "";
    [ProtoField(2)] public long Timestamp { get; set; }
}

[ProtoContract]
public class StreamItem
{
    [ProtoField(1)] public int Sequence { get; set; }
    [ProtoField(2)] public string Data { get; set; } = "";
}

[ProtoContract]
public class AggregateResult
{
    [ProtoField(1)] public int TotalItems { get; set; }
    [ProtoField(2)] public string Summary { get; set; } = "";
}

// ── Service interfaces ──

[ProtoService("PingService")]
public interface IPingService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<PingResponse> Ping(PingRequest request, CancellationToken ct);
}

[ProtoService("StreamService")]
public interface IStreamService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<PingResponse> Echo(PingRequest request);

    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    IAsyncEnumerable<StreamItem> GetStream(PingRequest request, CancellationToken ct);

    [ProtoMethod(ProtoMethodType.ClientStreaming)]
    Task<AggregateResult> Aggregate(IAsyncEnumerable<StreamItem> stream, CancellationToken ct);

    [ProtoMethod(ProtoMethodType.DuplexStreaming)]
    IAsyncEnumerable<StreamItem> BiDirectional(IAsyncEnumerable<StreamItem> stream, CancellationToken ct);
}

// No attribute — for negative tests
public interface INotAService
{
    Task<PingResponse> DoSomething(PingRequest request);
}

// Service implementation for discovery tests
public class PingServiceImpl : IPingService
{
    public Task<PingResponse> Ping(PingRequest request, CancellationToken ct)
        => Task.FromResult(new PingResponse { Reply = $"Pong: {request.Message}", Timestamp = 42 });
}

public class StreamServiceImpl : IStreamService
{
    public Task<PingResponse> Echo(PingRequest request)
        => Task.FromResult(new PingResponse { Reply = request.Message });

    public async IAsyncEnumerable<StreamItem> GetStream(PingRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new StreamItem { Sequence = i, Data = request.Message };
            await Task.Delay(1, ct);
        }
    }

    public async Task<AggregateResult> Aggregate(IAsyncEnumerable<StreamItem> stream, CancellationToken ct)
    {
        int count = 0;
        await foreach (var item in stream.WithCancellation(ct))
            count++;
        return new AggregateResult { TotalItems = count, Summary = "done" };
    }

    public async IAsyncEnumerable<StreamItem> BiDirectional(IAsyncEnumerable<StreamItem> stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in stream.WithCancellation(ct))
            yield return new StreamItem { Sequence = item.Sequence * 2, Data = item.Data };
    }
}
