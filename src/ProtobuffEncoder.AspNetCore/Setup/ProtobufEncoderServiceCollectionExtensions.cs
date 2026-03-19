using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ProtobuffEncoder.AspNetCore.Setup;

/// <summary>
/// Entry point for registering the ProtobuffEncoder framework with dependency injection.
/// </summary>
public static class ProtobufEncoderServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ProtobuffEncoder framework and returns a <see cref="ProtobufEncoderBuilder"/>
    /// for fluent transport configuration.
    /// <para>
    /// Configure transports by chaining <c>.WithRestFormatters()</c>, <c>.WithWebSocket()</c>,
    /// <c>.WithGrpc()</c>, or <c>.AddTransport()</c>.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// // Minimal — REST only
    /// builder.Services.AddProtobuffEncoder()
    ///     .WithRestFormatters();
    ///
    /// // Full stack — REST + WebSocket + gRPC
    /// builder.Services.AddProtobuffEncoder(options =>
    /// {
    ///     options.DefaultInvalidMessageBehavior = InvalidMessageBehavior.Skip;
    /// })
    /// .WithRestFormatters()
    /// .WithWebSocket(ws => ws
    ///     .AddEndpoint&lt;NotificationMessage, NotificationMessage&gt;())
    /// .WithGrpc(grpc => grpc
    ///     .AddService&lt;WeatherGrpcServiceImpl&gt;());
    /// </code>
    /// </example>
    public static ProtobufEncoderBuilder AddProtobuffEncoder(
        this IServiceCollection services,
        Action<ProtobufEncoderOptions>? configure = null)
    {
        var options = new ProtobufEncoderOptions();
        configure?.Invoke(options);

        // Register as IOptions<T> for injection
        services.TryAddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.TryAddSingleton(options);

        var encoderBuilder = new ProtobufEncoderBuilder(services, options);

        // Auto-add REST formatters when enabled via options
        if (options.EnableMvcFormatters)
            encoderBuilder.WithRestFormatters();

        // Register the builder itself so MapProtobufEndpoints() can access it
        services.TryAddSingleton(encoderBuilder);

        return encoderBuilder;
    }
}
