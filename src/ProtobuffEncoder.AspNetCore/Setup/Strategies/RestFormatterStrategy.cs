using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ProtobuffEncoder.AspNetCore.Setup.Strategies;

/// <summary>
/// Registers the <c>application/x-protobuf</c> MVC input/output formatters so controllers
/// and minimal APIs can accept and return protobuf-encoded request/response bodies.
/// </summary>
public sealed class RestFormatterStrategy : IProtobufTransportStrategy
{
    public void ConfigureServices(IServiceCollection services, ProtobufEncoderOptions options)
    {
        services.AddControllers().AddProtobufFormatters();
    }

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints, ProtobufEncoderOptions options) { }
}
