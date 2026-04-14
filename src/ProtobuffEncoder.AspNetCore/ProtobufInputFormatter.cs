using System.Buffers;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace ProtobuffEncoder.AspNetCore;

/// <summary>
/// ASP.NET Core MVC input formatter that reads a protobuf binary request body
/// and deserialises it into the declared model type.
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="PipeReader"/> to drain the request body into a single contiguous
/// <see cref="ReadOnlySequence{T}"/> without allocating a <see cref="MemoryStream"/>.
/// For single-segment bodies (the common case) the payload is decoded directly
/// from the pipe buffer — zero extra heap allocations for the byte array.
/// </para>
/// <para>
/// Responds to the <c>application/x-protobuf</c> media type.
/// Register via <see cref="ServiceCollectionExtensions.AddProtobufFormatters"/>.
/// </para>
/// </remarks>
/// <example>
/// MVC controller receiving a protobuf body:
/// <code>
/// [HttpPost("weather")]
/// public IActionResult Post([FromBody] WeatherRequest request) { … }
/// </code>
/// The client must send <c>Content-Type: application/x-protobuf</c>.
/// </example>
public sealed class ProtobufInputFormatter : InputFormatter
{
    /// <inheritdoc/>
    public ProtobufInputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(ProtobufMediaType.Protobuf));
    }

    /// <inheritdoc/>
    protected override bool CanReadType(Type type) =>
        type.GetConstructor(Type.EmptyTypes) is not null;

    /// <inheritdoc/>
    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        PipeReader reader = context.HttpContext.Request.BodyReader;
        CancellationToken cancellationToken = context.HttpContext.RequestAborted;

        byte[]? rented = null;
        int totalLength = 0;

        try
        {
            while (true)
            {
                ReadResult readResult = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = readResult.Buffer;

                if (!buffer.IsEmpty)
                {
                    checked
                    {
                        int bufferLength = (int)buffer.Length;

                        if (readResult.IsCompleted && rented is null)
                        {
                            ReadOnlySpan<byte> span = buffer.IsSingleSegment
                                ? buffer.FirstSpan
                                : buffer.ToArray();

                            object? model = ProtobufEncoder.Decode(context.ModelType, span);
                            return await InputFormatterResult.SuccessAsync(model);
                        }

                        if (rented is null)
                        {
                            rented = ArrayPool<byte>.Shared.Rent(Math.Max(bufferLength, 4096));
                        }
                        else if (rented.Length - totalLength < bufferLength)
                        {
                            int required = checked(totalLength + bufferLength);
                            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(required);

                            rented.AsSpan(0, totalLength).CopyTo(newBuffer);
                            ArrayPool<byte>.Shared.Return(rented);
                            rented = newBuffer;
                        }

                        buffer.CopyTo(rented.AsSpan(totalLength));
                        totalLength += bufferLength;
                    }
                }

                reader.AdvanceTo(buffer.End);

                if (readResult.IsCompleted)
                {
                    if (totalLength == 0)
                        return await InputFormatterResult.NoValueAsync();

                    object? model = ProtobufEncoder.Decode(
                        context.ModelType,
                        rented.AsSpan(0, totalLength));

                    return await InputFormatterResult.SuccessAsync(model);
                }
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
