using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using ProtobuffEncoder.Grpc.Client;
using ProtobuffEncoder.Grpc.Server;
using ProtobuffEncoder.Grpc.Tests.Fixtures;

namespace ProtobuffEncoder.Grpc.Tests;

/// <summary>
/// Tests for GrpcChannelExtensions, GrpcServiceExtensions, and GrpcClientServiceCollectionExtensions.
/// </summary>
public class GrpcExtensionsTests
{
    #region Simple-Test Pattern — CreateProtobufClient validation

    [Fact]
    public void CreateProtobufClient_NonInterface_ThrowsArgumentException()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost:5000");

        Assert.Throws<ArgumentException>(() =>
            channel.CreateProtobufClient<PingServiceImpl>());
    }

    [Fact]
    public void CreateProtobufClient_InterfaceWithoutAttribute_ThrowsArgumentException()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost:5000");

        Assert.Throws<ArgumentException>(() =>
            channel.CreateProtobufClient<INotAService>());
    }

    [Fact]
    public void CreateProtobufClient_ValidInterface_ReturnsProxy()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost:5000");

        var client = channel.CreateProtobufClient<IPingService>();

        Assert.NotNull(client);
        Assert.IsAssignableFrom<IPingService>(client);
    }

    [Fact]
    public void CreateProtobufClient_StreamServiceInterface_ReturnsProxy()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost:5000");

        var client = channel.CreateProtobufClient<IStreamService>();

        Assert.NotNull(client);
        Assert.IsAssignableFrom<IStreamService>(client);
    }

    #endregion

    #region Simple-Test Pattern — AddProtobufGrpcService DI registration

    [Fact]
    public void AddProtobufGrpcService_RegistersServiceAsScoped()
    {
        var services = new ServiceCollection();
        services.AddProtobufGrpcService<PingServiceImpl>();

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(PingServiceImpl));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
    }

    [Fact]
    public void AddProtobufGrpcService_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddProtobufGrpcService<PingServiceImpl>();

        Assert.Same(services, result);
    }

    #endregion

    #region Process-Rule Pattern — DI client registration

    [Fact]
    public void AddProtobufGrpcClient_UriOverload_RegistersSingleton()
    {
        var services = new ServiceCollection();
        services.AddProtobufGrpcClient<IPingService>(new Uri("http://localhost:5000"));

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IPingService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor!.Lifetime);
    }

    [Fact]
    public void AddProtobufGrpcClient_StringOverload_RegistersSingleton()
    {
        var services = new ServiceCollection();
        services.AddProtobufGrpcClient<IPingService>("http://localhost:5000");

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IPingService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor!.Lifetime);
    }

    [Fact]
    public void AddProtobufGrpcClient_WithConfigure_AcceptsCallback()
    {
        var services = new ServiceCollection();
        bool configured = false;

        services.AddProtobufGrpcClient<IPingService>(
            new Uri("http://localhost:5000"),
            opts => configured = true);

        // Configuration is deferred until resolution, but registration should succeed
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IPingService));
        Assert.NotNull(descriptor);
    }

    #endregion

    #region Constraint-Data Pattern — duplicate registrations

    [Fact]
    public void AddProtobufGrpcService_DuplicateCall_DoesNotDuplicate()
    {
        var services = new ServiceCollection();
        services.AddProtobufGrpcService<PingServiceImpl>();
        services.AddProtobufGrpcService<PingServiceImpl>();

        // TryAddScoped should prevent duplicates
        var count = services.Count(s => s.ServiceType == typeof(PingServiceImpl));
        Assert.Equal(1, count);
    }

    #endregion
}
