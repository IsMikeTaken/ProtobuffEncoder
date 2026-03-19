using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ProtobuffEncoder.AspNetCore.Setup.Strategies;

namespace ProtobuffEncoder.AspNetCore.Setup;

/// <summary>
/// Fluent builder for configuring the ProtobuffEncoder framework in an ASP.NET Core application.
/// Returned by <see cref="ProtobufEncoderServiceCollectionExtensions.AddProtobuffEncoder"/>.
/// <para>
/// Follows the strategy pattern: each transport (REST, WebSocket, gRPC) is an
/// <see cref="IProtobufTransportStrategy"/> that encapsulates its own DI and endpoint setup.
/// Use the convenience methods or register custom strategies.
/// </para>
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddProtobuffEncoder(options =>
/// {
///     options.EnableMvcFormatters = true;
///     options.DefaultInvalidMessageBehavior = InvalidMessageBehavior.Skip;
/// })
/// .WithRestFormatters()
/// .WithWebSocket(ws => ws
///     .AddEndpoint&lt;NotificationMessage, NotificationMessage&gt;()
///     .AddEndpoint&lt;WeatherResponse, WeatherRequest&gt;())
/// .WithGrpc(grpc => grpc
///     .AddService&lt;WeatherGrpcServiceImpl&gt;()
///     .AddService&lt;ChatGrpcServiceImpl&gt;());
/// </code>
/// </example>
public sealed class ProtobufEncoderBuilder
{
    private readonly IServiceCollection _services;
    private readonly ProtobufEncoderOptions _options;
    private readonly List<IProtobufTransportStrategy> _strategies = [];

    internal ProtobufEncoderBuilder(IServiceCollection services, ProtobufEncoderOptions options)
    {
        _services = services;
        _options = options;
    }

    /// <summary>
    /// Adds the <c>application/x-protobuf</c> MVC formatters for REST APIs.
    /// Controllers and minimal APIs will accept and return protobuf-encoded bodies.
    /// </summary>
    public ProtobufEncoderBuilder WithRestFormatters()
    {
        AddTransport(new RestFormatterStrategy());
        return this;
    }

    /// <summary>
    /// Configures protobuf WebSocket transports with connection managers and endpoint type pairs.
    /// </summary>
    /// <example>
    /// <code>
    /// .WithWebSocket(ws => ws
    ///     .AddEndpoint&lt;NotificationMessage, NotificationMessage&gt;())
    /// </code>
    /// </example>
    public ProtobufEncoderBuilder WithWebSocket(Action<WebSocketStrategy> configure)
    {
        var strategy = new WebSocketStrategy();
        configure(strategy);
        AddTransport(strategy);
        return this;
    }

    /// <summary>
    /// Configures protobuf gRPC services (code-first, no .proto files).
    /// </summary>
    /// <example>
    /// <code>
    /// .WithGrpc(grpc => grpc
    ///     .AddService&lt;WeatherGrpcServiceImpl&gt;()
    ///     .AddService&lt;ChatGrpcServiceImpl&gt;())
    /// </code>
    /// </example>
    public ProtobufEncoderBuilder WithGrpc(Action<GrpcStrategy> configure)
    {
        var strategy = new GrpcStrategy();
        configure(strategy);
        AddTransport(strategy);
        return this;
    }

    /// <summary>
    /// Registers a custom transport strategy for extensibility.
    /// Services are registered immediately so they are available when the DI container is built.
    /// </summary>
    public ProtobufEncoderBuilder AddTransport(IProtobufTransportStrategy strategy)
    {
        _strategies.Add(strategy);
        strategy.ConfigureServices(_services, _options);
        return this;
    }

    /// <summary>
    /// Maps all auto-mapped endpoints. Call from your <c>app</c> pipeline.
    /// </summary>
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        foreach (var strategy in _strategies)
            strategy.ConfigureEndpoints(endpoints, _options);
    }

    /// <summary>The registered strategies (for inspection/testing).</summary>
    public IReadOnlyList<IProtobufTransportStrategy> Strategies => _strategies.AsReadOnly();
}
