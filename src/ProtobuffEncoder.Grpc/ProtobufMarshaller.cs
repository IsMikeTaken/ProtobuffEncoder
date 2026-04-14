using System.Buffers;
using Grpc.Core;

namespace ProtobuffEncoder.Grpc;

/// <summary>
/// Creates gRPC <see cref="Marshaller{T}"/> instances that use <see cref="ProtobufEncoder"/>
/// for serialisation, bridging the ProtobuffEncoder library into the gRPC pipeline without
/// requiring <c>.proto</c> files or code generation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Serialisation</strong> — because <see cref="ProtobufEncoder"/> already produces a
/// <c>byte[]</c>, the serializer uses <see cref="SerializationContext.Complete(byte[])"/>.
/// This is compatible with both real gRPC pipelines and tests that invoke the byte[] serializer.
/// </para>
/// <para>
/// <strong>Deserialisation</strong> — prefers sequence-based access when available, and falls
/// back to <see cref="DeserializationContext.PayloadAsNewBuffer"/> for compatibility with test
/// contexts and older implementations.
/// </para>
/// </remarks>
public static class ProtobufMarshaller
{
    /// <summary>
    /// Creates a context-aware <see cref="Marshaller{T}"/> for <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The message type to marshal. Must be a reference type.</typeparam>
    public static Marshaller<T> Create<T>() where T : class
        => new(
            serializer: static message => ProtobufEncoder.Encode(message),
            deserializer: static payload => (T)ProtobufEncoder.Decode(typeof(T), payload));
}