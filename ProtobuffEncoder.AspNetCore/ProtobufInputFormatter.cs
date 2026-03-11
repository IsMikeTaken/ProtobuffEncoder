using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace ProtobuffEncoder.AspNetCore;

/// <summary>
/// ASP.NET Core input formatter that reads protobuf binary request bodies.
/// </summary>
public sealed class ProtobufInputFormatter : InputFormatter
{
    public ProtobufInputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(ProtobufMediaType.Protobuf));
    }

    protected override bool CanReadType(Type type)
    {
        return type.GetConstructor(Type.EmptyTypes) is not null;
    }

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        var request = context.HttpContext.Request;
        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms);

        var data = ms.ToArray();
        if (data.Length == 0)
            return await InputFormatterResult.NoValueAsync();

        var result = ProtobufEncoder.Decode(context.ModelType, data);
        return await InputFormatterResult.SuccessAsync(result);
    }
}
