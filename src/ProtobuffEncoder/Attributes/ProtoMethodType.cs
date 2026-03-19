namespace ProtobuffEncoder.Attributes;

/// <summary>
/// The type of gRPC method, matching the standard gRPC method classification.
/// </summary>
public enum ProtoMethodType
{
    /// <summary>Single request, single response.</summary>
    Unary,

    /// <summary>Single request, stream of responses.</summary>
    ServerStreaming,

    /// <summary>Stream of requests, single response.</summary>
    ClientStreaming,

    /// <summary>Stream of requests, stream of responses.</summary>
    DuplexStreaming
}
