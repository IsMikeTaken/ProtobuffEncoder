using System.Reflection;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Grpc;

/// <summary>
/// Describes a single gRPC method discovered from a <see cref="ProtoServiceAttribute"/>-decorated interface.
/// Contains the reflected metadata needed to create gRPC <see cref="Grpc.Core.Method{TRequest, TResponse}"/>
/// descriptors and handler adapters.
/// </summary>
internal sealed class ServiceMethodDescriptor
{
    public required string ServiceName { get; init; }
    public required string MethodName { get; init; }
    public required ProtoMethodType MethodType { get; init; }
    public required Type RequestType { get; init; }
    public required Type ResponseType { get; init; }
    public required MethodInfo InterfaceMethod { get; init; }
    public required MethodInfo ImplementationMethod { get; init; }
    public required bool HasCancellationToken { get; init; }

    /// <summary>
    /// Discovers all gRPC methods on a service implementation type by scanning its
    /// <see cref="ProtoServiceAttribute"/>-decorated interfaces.
    /// </summary>
    public static IReadOnlyList<ServiceMethodDescriptor> Discover(Type serviceType)
    {
        var descriptors = new List<ServiceMethodDescriptor>();

        foreach (var iface in serviceType.GetInterfaces())
        {
            var serviceAttr = iface.GetCustomAttribute<ProtoServiceAttribute>();
            if (serviceAttr is null) continue;

            var map = serviceType.GetInterfaceMap(iface);

            for (int i = 0; i < map.InterfaceMethods.Length; i++)
            {
                var ifaceMethod = map.InterfaceMethods[i];
                var implMethod = map.TargetMethods[i];

                var methodAttr = ifaceMethod.GetCustomAttribute<ProtoMethodAttribute>();
                if (methodAttr is null) continue;

                var (requestType, responseType) = ExtractTypes(ifaceMethod, methodAttr.MethodType);

                descriptors.Add(new ServiceMethodDescriptor
                {
                    ServiceName = serviceAttr.ServiceName,
                    MethodName = methodAttr.Name ?? ifaceMethod.Name,
                    MethodType = methodAttr.MethodType,
                    RequestType = requestType,
                    ResponseType = responseType,
                    InterfaceMethod = ifaceMethod,
                    ImplementationMethod = implMethod,
                    HasCancellationToken = ifaceMethod.GetParameters()
                        .Any(p => p.ParameterType == typeof(CancellationToken))
                });
            }
        }

        return descriptors;
    }

    /// <summary>
    /// Discovers methods directly on an interface (for client proxy generation where
    /// there is no implementation type, only the service interface).
    /// </summary>
    public static IReadOnlyList<ServiceMethodDescriptor> Discover(Type interfaceType, bool isInterfaceOnly)
    {
        if (!isInterfaceOnly) return Discover(interfaceType);

        var descriptors = new List<ServiceMethodDescriptor>();
        var serviceAttr = interfaceType.GetCustomAttribute<ProtoServiceAttribute>();
        if (serviceAttr is null) return descriptors;

        foreach (var method in interfaceType.GetMethods())
        {
            var methodAttr = method.GetCustomAttribute<ProtoMethodAttribute>();
            if (methodAttr is null) continue;

            var (requestType, responseType) = ExtractTypes(method, methodAttr.MethodType);

            descriptors.Add(new ServiceMethodDescriptor
            {
                ServiceName = serviceAttr.ServiceName,
                MethodName = methodAttr.Name ?? method.Name,
                MethodType = methodAttr.MethodType,
                RequestType = requestType,
                ResponseType = responseType,
                InterfaceMethod = method,
                ImplementationMethod = method, // Same for interface-only
                HasCancellationToken = method.GetParameters()
                    .Any(p => p.ParameterType == typeof(CancellationToken))
            });
        }

        return descriptors;
    }

    /// <summary>
    /// Extracts TRequest and TResponse from the method signature based on the method type.
    /// </summary>
    private static (Type request, Type response) ExtractTypes(MethodInfo method, ProtoMethodType methodType)
    {
        var parameters = method.GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken))
            .ToArray();

        Type requestType;
        Type responseType;

        switch (methodType)
        {
            case ProtoMethodType.Unary:
                // Task<TResponse> Method(TRequest request)
                requestType = parameters[0].ParameterType;
                responseType = method.ReturnType.GetGenericArguments()[0]; // Task<T> → T
                break;

            case ProtoMethodType.ServerStreaming:
                // IAsyncEnumerable<TResponse> Method(TRequest request, CancellationToken ct)
                requestType = parameters[0].ParameterType;
                responseType = method.ReturnType.GetGenericArguments()[0]; // IAsyncEnumerable<T> → T
                break;

            case ProtoMethodType.ClientStreaming:
                // Task<TResponse> Method(IAsyncEnumerable<TRequest> stream, CancellationToken ct)
                requestType = parameters[0].ParameterType.GetGenericArguments()[0]; // IAsyncEnumerable<T> → T
                responseType = method.ReturnType.GetGenericArguments()[0]; // Task<T> → T
                break;

            case ProtoMethodType.DuplexStreaming:
                // IAsyncEnumerable<TResponse> Method(IAsyncEnumerable<TRequest> stream, CancellationToken ct)
                requestType = parameters[0].ParameterType.GetGenericArguments()[0]; // IAsyncEnumerable<T> → T
                responseType = method.ReturnType.GetGenericArguments()[0]; // IAsyncEnumerable<T> → T
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(methodType));
        }

        return (requestType, responseType);
    }
}
