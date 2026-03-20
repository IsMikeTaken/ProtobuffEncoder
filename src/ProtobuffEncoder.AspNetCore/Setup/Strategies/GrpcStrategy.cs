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
    private readonly List<int> _extraGrpcPorts = [];

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
    /// Adds a general port that will exclusively handle HTTP/2 gRPC traffic, decoded automatically.
    /// This makes it easy to bind multiple or arbitrary hardware ports to the gRPC services.
    /// </summary>
    public GrpcStrategy AddGrpcPort(int port)
    {
        _extraGrpcPorts.Add(port);
        return this;
    }

    /// <summary>
    /// Registers all classes in the given assembly that implement a <see cref="Attributes.ProtoServiceAttribute"/> interface.
    /// Eliminates the need to manually call AddService&lt;T&gt;() for every service.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="autoMap">When true, endpoints are mapped automatically.</param>
    public GrpcStrategy AddServiceAssembly(System.Reflection.Assembly assembly, bool autoMap = true)
    {
        var types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && 
                        (t.GetCustomAttributes(typeof(Attributes.ProtoServiceAttribute), true).Length > 0 ||
                         t.GetInterfaces().Any(i => i.GetCustomAttributes(typeof(Attributes.ProtoServiceAttribute), true).Length > 0)))
            .ToList();

        foreach (var type in types)
        {
            // Call AddService<TService>() via reflection
            var method = typeof(GrpcStrategy).GetMethod(nameof(AddService))!
                .MakeGenericMethod(type);
            method.Invoke(this, [autoMap]);
        }

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

        if (_httpPort.HasValue || _grpcPort.HasValue || _extraGrpcPorts.Count > 0)
        {
            services.Configure<KestrelServerOptions>(kestrel =>
            {
                if (_httpPort.HasValue)
                    kestrel.ListenLocalhost(_httpPort.Value, o => o.Protocols = HttpProtocols.Http1);
                
                if (_grpcPort.HasValue)
                    kestrel.ListenLocalhost(_grpcPort.Value, o => o.Protocols = HttpProtocols.Http2);

                foreach (var port in _extraGrpcPorts)
                {
                    kestrel.ListenLocalhost(port, o => o.Protocols = HttpProtocols.Http2);
                }
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
