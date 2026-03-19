using System.Reflection;
using System.Runtime.CompilerServices;
using Grpc.Core;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Grpc.Client;

/// <summary>
/// A <see cref="DispatchProxy"/>-based runtime proxy that implements a
/// <see cref="ProtoServiceAttribute"/>-decorated interface by dispatching
/// calls through a gRPC <see cref="CallInvoker"/> with <see cref="ProtobufMarshaller"/>.
/// <para>
/// Supports all four gRPC method types: Unary, ServerStreaming, ClientStreaming, DuplexStreaming.
/// Handlers are pre-built at initialization time so <see cref="Invoke"/> is just a dictionary lookup.
/// </para>
/// </summary>
public class ProtobufGrpcClientProxy : DispatchProxy
{
    private CallInvoker _invoker = null!;
    private readonly Dictionary<MethodInfo, Func<object?[]?, object?>> _handlers = new();

    internal void Initialize(CallInvoker invoker, Type serviceInterface)
    {
        _invoker = invoker;
        var descriptors = ServiceMethodDescriptor.Discover(serviceInterface, isInterfaceOnly: true);

        foreach (var desc in descriptors)
        {
            var builderName = desc.MethodType switch
            {
                ProtoMethodType.Unary => nameof(BuildUnaryHandler),
                ProtoMethodType.ServerStreaming => nameof(BuildServerStreamingHandler),
                ProtoMethodType.ClientStreaming => nameof(BuildClientStreamingHandler),
                ProtoMethodType.DuplexStreaming => nameof(BuildDuplexStreamingHandler),
                _ => throw new NotSupportedException()
            };

            var handler = (Func<object?[]?, object?>)
                typeof(ProtobufGrpcClientProxy)
                    .GetMethod(builderName, BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(desc.RequestType, desc.ResponseType)
                    .Invoke(this, [desc])!;

            _handlers[desc.InterfaceMethod] = handler;
        }
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
            throw new ArgumentNullException(nameof(targetMethod));

        if (_handlers.TryGetValue(targetMethod, out var handler))
            return handler(args);

        throw new NotSupportedException(
            $"Method '{targetMethod.Name}' is not a [ProtoMethod]-decorated gRPC method.");
    }

    #region Handler Builders

    private Func<object?[]?, object?> BuildUnaryHandler<TRequest, TResponse>(ServiceMethodDescriptor desc)
        where TRequest : class
        where TResponse : class
    {
        var grpcMethod = CreateMethod<TRequest, TResponse>(MethodType.Unary, desc);

        return args =>
        {
            var request = (TRequest)args![0]!;
            var ct = ExtractCancellationToken(args);
            var call = _invoker.AsyncUnaryCall(grpcMethod, host: null, new CallOptions(cancellationToken: ct), request);
            return call.ResponseAsync;
        };
    }

    private Func<object?[]?, object?> BuildServerStreamingHandler<TRequest, TResponse>(ServiceMethodDescriptor desc)
        where TRequest : class
        where TResponse : class
    {
        var grpcMethod = CreateMethod<TRequest, TResponse>(MethodType.ServerStreaming, desc);

        return args =>
        {
            var request = (TRequest)args![0]!;
            var ct = ExtractCancellationToken(args);
            var call = _invoker.AsyncServerStreamingCall(grpcMethod, host: null, new CallOptions(cancellationToken: ct), request);
            return call.ResponseStream.ReadAllAsync(ct);
        };
    }

    private Func<object?[]?, object?> BuildClientStreamingHandler<TRequest, TResponse>(ServiceMethodDescriptor desc)
        where TRequest : class
        where TResponse : class
    {
        var grpcMethod = CreateMethod<TRequest, TResponse>(MethodType.ClientStreaming, desc);

        return args =>
        {
            var inputStream = (IAsyncEnumerable<TRequest>)args![0]!;
            var ct = ExtractCancellationToken(args);
            return ClientStreamingCallAsync(grpcMethod, inputStream, ct);
        };
    }

    private Func<object?[]?, object?> BuildDuplexStreamingHandler<TRequest, TResponse>(ServiceMethodDescriptor desc)
        where TRequest : class
        where TResponse : class
    {
        var grpcMethod = CreateMethod<TRequest, TResponse>(MethodType.DuplexStreaming, desc);

        return args =>
        {
            var inputStream = (IAsyncEnumerable<TRequest>)args![0]!;
            var ct = ExtractCancellationToken(args);
            return DuplexStreamingCall(grpcMethod, inputStream, ct);
        };
    }

    #endregion

    #region Streaming Helpers

    private async Task<TResponse> ClientStreamingCallAsync<TRequest, TResponse>(
        Method<TRequest, TResponse> grpcMethod,
        IAsyncEnumerable<TRequest> inputStream,
        CancellationToken ct)
        where TRequest : class
        where TResponse : class
    {
        var call = _invoker.AsyncClientStreamingCall(grpcMethod, host: null, new CallOptions(cancellationToken: ct));

        await foreach (var item in inputStream.WithCancellation(ct))
        {
            await call.RequestStream.WriteAsync(item);
        }

        await call.RequestStream.CompleteAsync();
        return await call.ResponseAsync;
    }

    private async IAsyncEnumerable<TResponse> DuplexStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> grpcMethod,
        IAsyncEnumerable<TRequest> inputStream,
        [EnumeratorCancellation] CancellationToken ct)
        where TRequest : class
        where TResponse : class
    {
        var call = _invoker.AsyncDuplexStreamingCall(grpcMethod, host: null, new CallOptions(cancellationToken: ct));

        // Start writing input to the request stream in background
        var writeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in inputStream.WithCancellation(ct))
                {
                    await call.RequestStream.WriteAsync(item);
                }
            }
            finally
            {
                await call.RequestStream.CompleteAsync();
            }
        }, ct);

        // Yield responses as they arrive
        await foreach (var response in call.ResponseStream.ReadAllAsync(ct))
        {
            yield return response;
        }

        await writeTask; // Ensure write task completed cleanly
    }

    #endregion

    private static Method<TRequest, TResponse> CreateMethod<TRequest, TResponse>(
        MethodType type,
        ServiceMethodDescriptor desc)
        where TRequest : class
        where TResponse : class
        => new(type, desc.ServiceName, desc.MethodName,
            ProtobufMarshaller.Create<TRequest>(),
            ProtobufMarshaller.Create<TResponse>());

    private static CancellationToken ExtractCancellationToken(object?[]? args)
    {
        if (args is null) return default;
        for (int i = args.Length - 1; i >= 0; i--)
        {
            if (args[i] is CancellationToken ct) return ct;
        }
        return default;
    }
}
