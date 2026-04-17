using System.IO.Pipelines;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace ProtobuffEncoder.AspNetCore;

/// <summary>
/// ASP.NET Core MVC output formatter that serialises the response object to
/// protobuf binary and writes it directly to the response body pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Encodes the model to a <c>byte[]</c> and writes it through
/// <see cref="PipeWriter"/> so Kestrel can flush the data without an
/// intermediate copy. Response content length is set
/// so clients and proxies can pre-allocate receive buffers.
/// </para>
/// <para>
/// Responds to the <c>application/x-protobuf</c> media type.
/// Register via <see cref="ServiceCollectionExtensions.AddProtobufFormatters"/>.
/// </para>
/// </remarks>
/// <example>
/// MVC controller returning a protobuf response:
/// <code>
/// [HttpGet("forecast")]
/// [Produces(ProtobufMediaType.Protobuf)]
/// public WeatherForecast Get() => new() { Temperature = 22 };
/// </code>
/// </example>
public sealed class ProtobufOutputFormatter : OutputFormatter
{
    /// <inheritdoc/>
    public ProtobufOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(ProtobufMediaType.Protobuf));
    }

    /// <inheritdoc/>
    protected override bool CanWriteType(Type? type) => type is not null;

    /// <inheritdoc/>
    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
    {
        if (context.Object is null)
            return;

        byte[] bytes = ProtobufEncoder.Encode(context.Object);
        context.HttpContext.Response.ContentLength = bytes.Length;

        // Write directly into the Kestrel PipeWriter — avoids a Stream.WriteAsync
        // call and lets Kestrel flush from the pooled pipe memory.
        PipeWriter writer = context.HttpContext.Response.BodyWriter;
        await writer.WriteAsync(bytes, context.HttpContext.RequestAborted);
    }
}

