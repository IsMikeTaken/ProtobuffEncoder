using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ProtobuffEncoder.AspNetCore.Setup.Strategies;

/// <summary>
/// Registers protobuf-encoded gRPC services. Each service implementation is registered
/// with its <c>IServiceMethodProvider&lt;T&gt;</c> so the standard <c>MapGrpcService&lt;T&gt;()</c>
/// discovers methods via <see cref="Attributes.ProtoServiceAttribute"/> reflection.
/// </summary>
public sealed class GrpcStrategy : IProtobufTransportStrategy
{
    private readonly List<Action<IServiceCollection>> _registrations = [];
    private readonly List<Action<IEndpointRouteBuilder>> _mappings = [];
    private bool _grpcAdded;
    private int? _httpPort;
    private int? _grpcPort;

    /// <summary>
    /// Configures Kestrel endpoints for serving both HTTP/1.1 (browser, REST) and
    /// HTTP/2 (gRPC) without TLS. This creates two separate listen endpoints because
    /// Kestrel cannot negotiate HTTP/2 over cleartext — only TLS supports ALPN negotiation.
    /// <para>
    /// The <paramref name="httpPort"/> listens with <see cref="HttpProtocols.Http1"/>
    /// for browser dashboards and REST APIs. The <paramref name="grpcPort"/> listens with
    /// <see cref="HttpProtocols.Http2"/> for gRPC calls.
    /// </para>
    /// <para>
    /// When using HTTPS, both protocols are negotiated via ALPN on a single port and
    /// this method is not needed.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// .WithGrpc(grpc => grpc
    ///     .UseKestrel(httpPort: 5400, grpcPort: 5401)
    ///     .AddService&lt;WeatherGrpcServiceImpl&gt;())
    /// </code>
    /// </example>
    /// <param name="httpPort">Port for HTTP/1.1 traffic (browser dashboard, REST APIs).</param>
    /// <param name="grpcPort">Port for HTTP/2 traffic (gRPC calls).</param>
    public GrpcStrategy UseKestrel(int httpPort, int grpcPort)
    {
        _httpPort = httpPort;
        _grpcPort = grpcPort;
        return this;
    }

    /// <summary>
    /// Registers a gRPC service implementation and optionally auto-maps its endpoint.
    /// </summary>
    /// <param name="autoMap">
    /// When true (default), the service endpoint is also mapped during
    /// <see cref="ConfigureEndpoints"/>. Set to false if you want to call
    /// <c>app.MapGrpcService&lt;T&gt;()</c> manually.
    /// </param>
    public GrpcStrategy AddService<TService>(bool autoMap = true)
        where TService : class
    {
        _registrations.Add(services =>
            Grpc.Server.GrpcServiceExtensions.AddProtobufGrpcService<TService>(services));

        if (autoMap)
        {
            _mappings.Add(endpoints =>
                endpoints.MapGrpcService<TService>());
        }

        return this;
    }

    public void ConfigureServices(IServiceCollection services, ProtobufEncoderOptions options)
    {
        if (!_grpcAdded)
        {
            services.AddGrpc();
            _grpcAdded = true;
        }

        if (_httpPort.HasValue && _grpcPort.HasValue)
        {
            var httpPort = _httpPort.Value;
            var grpcPort = _grpcPort.Value;
            services.Configure<KestrelServerOptions>(kestrel =>
            {
                kestrel.ListenLocalhost(httpPort, o => o.Protocols = HttpProtocols.Http1);
                kestrel.ListenLocalhost(grpcPort, o => o.Protocols = HttpProtocols.Http2);
            });
        }

        foreach (var registration in _registrations)
            registration(services);
    }

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints, ProtobufEncoderOptions options)
    {
        foreach (var mapping in _mappings)
            mapping(endpoints);
    }
}
