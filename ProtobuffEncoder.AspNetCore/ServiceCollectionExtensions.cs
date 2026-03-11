using Microsoft.Extensions.DependencyInjection;

namespace ProtobuffEncoder.AspNetCore;

/// <summary>
/// DI registration extensions for protobuf support in ASP.NET Core.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds protobuf input/output formatters to MVC so controllers and minimal APIs
    /// can accept and return <c>application/x-protobuf</c> bodies.
    /// </summary>
    public static IMvcBuilder AddProtobufFormatters(this IMvcBuilder builder)
    {
        builder.AddMvcOptions(options =>
        {
            options.InputFormatters.Insert(0, new ProtobufInputFormatter());
            options.OutputFormatters.Insert(0, new ProtobufOutputFormatter());
        });
        return builder;
    }
}
