using Microsoft.Extensions.Configuration;
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
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<ProtobufEncoderOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        var options = new ProtobufEncoderOptions();
        configure?.Invoke(options);
        return AddProtobuffEncoderCore(services, options);
    }

    /// <summary>
    /// Registers the ProtobuffEncoder framework using configuration-bound options from the provided section.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="section">The configuration section to bind to <see cref="ProtobufEncoderOptions"/>.</param>
    /// <returns>A fluent builder for configuring transports.</returns>
    public static ProtobufEncoderBuilder AddProtobuffEncoder(
        this IServiceCollection services,
        IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        services.AddOptions<ProtobufEncoderOptions>()
            .Bind(section);

        var options = new ProtobufEncoderOptions();
        section.Bind(options);
        return AddProtobuffEncoderCore(services, options);
    }

    /// <summary>
    /// Registers the ProtobuffEncoder framework using a named configuration section.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="configuration">The configuration root that contains the section.</param>
    /// <param name="sectionName">The section name. Defaults to <c>ProtobuffEncoder</c>.</param>
    /// <returns>A fluent builder for configuring transports.</returns>
    public static ProtobufEncoderBuilder AddProtobuffEncoder(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "ProtobuffEncoder")
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return AddProtobuffEncoder(services, configuration.GetSection(sectionName));
    }

    private static ProtobufEncoderBuilder AddProtobuffEncoderCore(
        IServiceCollection services,
        ProtobufEncoderOptions options)
    {
        services.TryAddSingleton(static serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<ProtobufEncoderOptions>>().Value);

        var encoderBuilder = new ProtobufEncoderBuilder(services, options);

        if (options.EnableMvcFormatters)
        {
            encoderBuilder.WithRestFormatters();
        }

        services.TryAddSingleton(encoderBuilder);
        return encoderBuilder;
    }
}
