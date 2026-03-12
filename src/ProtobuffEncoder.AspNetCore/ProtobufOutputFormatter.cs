using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace ProtobuffEncoder.AspNetCore;

/// <summary>
/// ASP.NET Core output formatter that writes protobuf binary response bodies.
/// </summary>
public sealed class ProtobufOutputFormatter : OutputFormatter
{
    public ProtobufOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(ProtobufMediaType.Protobuf));
    }

    protected override bool CanWriteType(Type? type)
    {
        return type is not null;
    }

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
    {
        if (context.Object is null)
            return;

        var bytes = ProtobufEncoder.Encode(context.Object);
        context.HttpContext.Response.ContentLength = bytes.Length;
        await context.HttpContext.Response.Body.WriteAsync(bytes);
    }
}
