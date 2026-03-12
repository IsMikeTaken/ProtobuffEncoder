namespace ProtobuffEncoder;

/// <summary>
/// A pre-compiled message handler that bundles encode and decode delegates for a specific type.
/// Descriptors are resolved once at creation time, avoiding repeated reflection overhead.
/// </summary>
/// <typeparam name="T">The protobuf contract type.</typeparam>
public sealed class StaticMessage<T> where T : class, new()
{
    private readonly Func<T, byte[]> _encode;
    private readonly Func<byte[], T> _decode;

    internal StaticMessage(Func<T, byte[]> encode, Func<byte[], T> decode)
    {
        _encode = encode;
        _decode = decode;
    }

    /// <summary>
    /// Encode an instance to protobuf bytes.
    /// </summary>
    public byte[] Encode(T instance) => _encode(instance);

    /// <summary>
    /// Decode protobuf bytes into an instance.
    /// </summary>
    public T Decode(byte[] data) => _decode(data);

    /// <summary>
    /// Encode and write a length-delimited message to a stream.
    /// </summary>
    public void WriteDelimited(T instance, Stream output)
    {
        ProtobufEncoder.WriteDelimitedMessage(instance, output);
    }

    /// <summary>
    /// Encode and write a length-delimited message to a stream asynchronously.
    /// </summary>
    public Task WriteDelimitedAsync(T instance, Stream output, CancellationToken cancellationToken = default)
    {
        return ProtobufEncoder.WriteDelimitedMessageAsync(instance, output, cancellationToken);
    }

    /// <summary>
    /// Read a length-delimited message from a stream. Returns null at end of stream.
    /// </summary>
    public T? ReadDelimited(Stream input)
    {
        return ProtobufEncoder.ReadDelimitedMessage<T>(input);
    }
}
