using Grpc.Core;

namespace ProtobuffEncoder.Grpc;

/// <summary>
/// Creates gRPC <see cref="Marshaller{T}"/> instances that use <see cref="ProtobufEncoder"/>
/// for serialization instead of Google.Protobuf. This bridges the ProtobuffEncoder library
/// into the gRPC pipeline without requiring .proto files or code generation.
/// </summary>
public static class ProtobufMarshaller
{
    /// <summary>
    /// Creates a <see cref="Marshaller{T}"/> for the given message type.
    /// Uses <see cref="ProtobufEncoder.Encode"/> for serialization and
    /// <see cref="ProtobufEncoder.Decode(Type, ReadOnlySpan{byte})"/> for deserialization.
    /// </summary>
    public static Marshaller<T> Create<T>() where T : class
        => new(
            serializer: msg => ProtobufEncoder.Encode(msg),
            deserializer: bytes => (T)ProtobufEncoder.Decode(typeof(T), bytes));
}
