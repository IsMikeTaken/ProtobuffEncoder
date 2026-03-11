using System.Net;
using System.Net.Http.Headers;

namespace ProtobuffEncoder.AspNetCore;

/// <summary>
/// HttpContent that serializes an object to protobuf binary for use with HttpClient.
/// </summary>
public sealed class ProtobufHttpContent : HttpContent
{
    private readonly byte[] _data;

    public ProtobufHttpContent(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _data = ProtobufEncoder.Encode(instance);
        Headers.ContentType = new MediaTypeHeaderValue(ProtobufMediaType.Protobuf);
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        return stream.WriteAsync(_data, 0, _data.Length);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _data.Length;
        return true;
    }
}
