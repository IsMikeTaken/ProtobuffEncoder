using System.Collections.Concurrent;
using System.Reflection;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Grpc.Server;

/// <summary>
/// Discovers gRPC methods on <typeparamref name="TService"/> by reflecting on
/// <see cref="ProtoServiceAttribute"/>-decorated interfaces and binds them to the
/// ASP.NET Core gRPC pipeline using <see cref="ProtobufMarshaller"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered via <see cref="GrpcServiceExtensions.AddProtobufGrpcService{TService}"/>
/// and invoked automatically by <c>MapGrpcService&lt;TService&gt;()</c>.
/// </para>
/// <para>
/// <strong>Reflection caching</strong> — the open-generic binder methods
/// (<c>BindUnary</c>, <c>BindServerStreaming</c>, etc.) are closed and compiled into
/// typed delegates exactly once per <typeparamref name="TService"/> / request-response
/// type pair using a <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed on the
/// <see cref="ServiceMethodDescriptor"/>.  Subsequent calls to
/// <see cref="OnServiceMethodDiscovery"/> (e.g. hot-reload, multi-registration) pay
/// only a dictionary lookup with no <see cref="MethodInfo.MakeGenericMethod"/> allocation.
/// </para>
/// </remarks>
internal sealed class ProtobufGrpcServiceMethodProvider<TService> : IServiceMethodProvider<TService>
    where TService : class
{
    // Key: (MethodName, RequestType, ResponseType, MethodType) — identifies a unique binder delegate.
    private static readonly ConcurrentDictionary<BinderKey, Action<ServiceMethodProviderContext<TService>, ServiceMethodDescriptor>>
        _binderCache = new();

    /// <inheritdoc/>
    public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TService> context)
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(TService));

        foreach (var desc in descriptors)
        {
            var key = new BinderKey(desc.ServiceName, desc.MethodName, desc.RequestType, desc.ResponseType, desc.MethodType);
            var binder = _binderCache.GetOrAdd(key, static k => BuildBinder<TService>(k));
            binder(context, desc);
        }
    }

    // ── Binder factory ────────────────────────────────────────────────────────

    /// <summary>
    /// Reflects once to close the appropriate generic binder method and compile it
    /// into a typed delegate. Called at most once per unique method signature.
    /// </summary>
    private static Action<ServiceMethodProviderContext<TService>, ServiceMethodDescriptor>
        BuildBinder<TSvc>(BinderKey key) where TSvc : class
    {
        string methodName = key.MethodType switch
        {
            ProtoMethodType.Unary => nameof(BindUnary),
            ProtoMethodType.ServerStreaming => nameof(BindServerStreaming),
            ProtoMethodType.ClientStreaming => nameof(BindClientStreaming),
            ProtoMethodType.DuplexStreaming => nameof(BindDuplexStreaming),
            _ => throw new NotSupportedException(
                                                  $"Method type {key.MethodType} is not supported")
        };

        var openMethod = typeof(ProtobufGrpcServiceMethodProvider<TService>)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(key.RequestType, key.ResponseType);

        // Compile once into a typed delegate — subsequent invocations are direct calls.
        return (Action<ServiceMethodProviderContext<TService>, ServiceMethodDescriptor>)
            Delegate.CreateDelegate(
                typeof(Action<ServiceMethodProviderContext<TService>, ServiceMethodDescriptor>),
                openMethod);
    }

    // ── Binders ───────────────────────────────────────────────────────────────

    private static void BindUnary<TRequest, TResponse>(
        ServiceMethodProviderContext<TService> context,
        ServiceMethodDescriptor desc)
        where TRequest : class
        where TResponse : class
    {
        var grpcMethod = CreateMethod<TRequest, TResponse>(MethodType.Unary, desc);

        context.AddUnaryMethod(grpcMethod, new List<object>(),
            (service, request, serverCallContext) =>
            {
                object? result = desc.HasCancellationToken
                    ? desc.ImplementationMethod.Invoke(service, [request, serverCallContext.CancellationToken])
                    : desc.ImplementationMethod.Invoke(service, [request]);

                return (Task<TResponse>)result!;
            });
    }

    private static void BindServerStreaming<TRequest, TResponse>(
        ServiceMethodProviderContext<TService> context,
        ServiceMethodDescriptor desc)
        where TRequest : class
        where TResponse : class
    {
        var grpcMethod = CreateMethod<TRequest, TResponse>(MethodType.ServerStreaming, desc);

        context.AddServerStreamingMethod(grpcMethod, new List<object>(),
            async (service, request, responseStream, serverCallContext) =>
            {
                object? result = desc.HasCancellationToken
                    ? desc.ImplementationMethod.Invoke(service, [request, serverCallContext.CancellationToken])
                    : desc.ImplementationMethod.Invoke(service, [request]);

                var enumerable = (IAsyncEnumerable<TResponse>)result!;

                await foreach (var item in enumerable.WithCancellation(serverCallContext.CancellationToken))
                    await responseStream.WriteAsync(item);
            });
    }

    private static void BindClientStreaming<TRequest, TResponse>(
        ServiceMethodProviderContext<TService> context,
        ServiceMethodDescriptor desc)
        where TRequest : class
        where TResponse : class
    {
        var grpcMethod = CreateMethod<TRequest, TResponse>(MethodType.ClientStreaming, desc);

        context.AddClientStreamingMethod(grpcMethod, new List<object>(),
            (service, requestStream, serverCallContext) =>
            {
                var enumerable = requestStream.ReadAllAsync(serverCallContext.CancellationToken);

                object? result = desc.HasCancellationToken
                    ? desc.ImplementationMethod.Invoke(service, [enumerable, serverCallContext.CancellationToken])
                    : desc.ImplementationMethod.Invoke(service, [enumerable]);

                return (Task<TResponse>)result!;
            });
    }

    private static void BindDuplexStreaming<TRequest, TResponse>(
        ServiceMethodProviderContext<TService> context,
        ServiceMethodDescriptor desc)
        where TRequest : class
        where TResponse : class
    {
        var grpcMethod = CreateMethod<TRequest, TResponse>(MethodType.DuplexStreaming, desc);

        context.AddDuplexStreamingMethod(grpcMethod, new List<object>(),
            async (service, requestStream, responseStream, serverCallContext) =>
            {
                var enumerable = requestStream.ReadAllAsync(serverCallContext.CancellationToken);

                object? result = desc.HasCancellationToken
                    ? desc.ImplementationMethod.Invoke(service, [enumerable, serverCallContext.CancellationToken])
                    : desc.ImplementationMethod.Invoke(service, [enumerable]);

                var outgoing = (IAsyncEnumerable<TResponse>)result!;

                await foreach (var item in outgoing.WithCancellation(serverCallContext.CancellationToken))
                    await responseStream.WriteAsync(item);
            });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Method<TRequest, TResponse> CreateMethod<TRequest, TResponse>(
        MethodType type,
        ServiceMethodDescriptor desc)
        where TRequest : class
        where TResponse : class
        => new(type, desc.ServiceName, desc.MethodName,
            ProtobufMarshaller.Create<TRequest>(),
            ProtobufMarshaller.Create<TResponse>());

    // ── Cache key ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Value-type key for the binder delegate cache so equality checks are allocation-free.
    /// </summary>
    private readonly record struct BinderKey(
        string ServiceName,
        string MethodName,
        Type RequestType,
        Type ResponseType,
        ProtoMethodType MethodType);
}
