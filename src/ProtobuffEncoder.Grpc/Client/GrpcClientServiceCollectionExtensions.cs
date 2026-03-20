using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;

namespace ProtobuffEncoder.Grpc.Client;

/// <summary>
/// Extension methods for setting up Protobuf gRPC clients simply within the Microsoft DI container.
/// </summary>
public static class GrpcClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers a protobuf-encoded gRPC client proxy as a Singleton in the service collection.
    /// Instances injected via the DI container will automatically have their connections managed.
    /// </summary>
    /// <typeparam name="TService">The interface decorated with [ProtoService].</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureClient">Configuration for the underlying GrpcChannelOptions.</param>
    /// <param name="address">The base URL of the gRPC server.</param>
    public static IServiceCollection AddProtobufGrpcClient<TService>(
        this IServiceCollection services,
        Uri address,
        Action<GrpcChannelOptions>? configureClient = null)
        where TService : class
    {
        services.AddSingleton(provider =>
        {
            var options = new GrpcChannelOptions { ServiceProvider = provider };
            configureClient?.Invoke(options);

            var channel = GrpcChannel.ForAddress(address, options);
            return channel.CreateProtobufClient<TService>();
        });

        return services;
    }

    /// <summary>
    /// Registers a protobuf-encoded gRPC client proxy as a Singleton in the service collection.
    /// </summary>
    /// <typeparam name="TService">The interface decorated with [ProtoService].</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="address">The base URL of the gRPC server as a string.</param>
    public static IServiceCollection AddProtobufGrpcClient<TService>(
        this IServiceCollection services,
        string address)
        where TService : class
    {
        return services.AddProtobufGrpcClient<TService>(new Uri(address));
    }
}
