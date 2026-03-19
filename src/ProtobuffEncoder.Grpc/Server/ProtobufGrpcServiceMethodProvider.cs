using System.Reflection;
using System.Runtime.CompilerServices;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Grpc.Server;

/// <summary>
/// Discovers gRPC methods on <typeparamref name="TService"/> by reflecting on
/// <see cref="ProtoServiceAttribute"/>-decorated interfaces and binds them to the
/// ASP.NET Core gRPC pipeline using <see cref="ProtobufMarshaller"/>.
/// <para>
/// Registered via <see cref="GrpcServiceExtensions.AddProtobufGrpcService{TService}"/>
/// and invoked automatically by <c>MapGrpcService&lt;TService&gt;()</c>.
/// </para>
/// </summary>
internal sealed class ProtobufGrpcServiceMethodProvider<TService> : IServiceMethodProvider<TService>
    where TService : class
{
    public void OnServiceMethodDiscovery(ServiceMethodProviderContext<TService> context)
    {
        var descriptors = ServiceMethodDescriptor.Discover(typeof(TService));

        foreach (var desc in descriptors)
        {
            // Call the typed binder via reflection to satisfy generic constraints on AddXxxMethod
            var binderName = desc.MethodType switch
            {
                ProtoMethodType.Unary => nameof(BindUnary),
                ProtoMethodType.ServerStreaming => nameof(BindServerStreaming),
                ProtoMethodType.ClientStreaming => nameof(BindClientStreaming),
                ProtoMethodType.DuplexStreaming => nameof(BindDuplexStreaming),
                _ => throw new NotSupportedException($"Method type {desc.MethodType} is not supported")
            };

            typeof(ProtobufGrpcServiceMethodProvider<TService>)
                .GetMethod(binderName, BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(desc.RequestType, desc.ResponseType)
                .Invoke(null, [context, desc]);
        }
    }

    #region Binders

    private static void BindUnary<TRequest, TResponse>(
        ServiceMethodProviderContext<TService> context,
        ServiceMethodDescriptor desc)
        where TRequest : class
        where TResponse : class
    {
        var grpcMethod = CreateMethod<TRequest, TResponse>(MethodType.Unary, desc);

        context.AddUnaryMethod(grpcMethod, new List<object>(),
            async (service, request, serverCallContext) =>
            {
                object? result = desc.HasCancellationToken
                    ? desc.ImplementationMethod.Invoke(service, [request, serverCallContext.CancellationToken])
                    : desc.ImplementationMethod.Invoke(service, [request]);

                return await (Task<TResponse>)result!;
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
                {
                    await responseStream.WriteAsync(item);
                }
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
            async (service, requestStream, serverCallContext) =>
            {
                var enumerable = requestStream.ReadAllAsync(serverCallContext.CancellationToken);

                object? result = desc.HasCancellationToken
                    ? desc.ImplementationMethod.Invoke(service, [enumerable, serverCallContext.CancellationToken])
                    : desc.ImplementationMethod.Invoke(service, [enumerable]);

                return await (Task<TResponse>)result!;
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
                {
                    await responseStream.WriteAsync(item);
                }
            });
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
}
