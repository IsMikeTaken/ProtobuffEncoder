using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a gRPC service definition in a .proto file.
/// </summary>
public sealed class ProtoServiceDef
{
    /// <summary>Service name (PascalCase, e.g. <c>OrderService</c>).</summary>
    public string Name { get; init; } = "";

    /// <summary>The originating CLR interface type, used for traceability comments.</summary>
    public Type? SourceType { get; init; }

    /// <summary>Optional human-readable comment placed above the service block.</summary>
    public string? Metadata { get; init; }

    /// <summary>
    /// When <see langword="true"/> emits <c>option deprecated = true;</c> inside the service block.
    /// </summary>
    public bool IsDeprecated { get; init; }

    /// <summary>RPC method definitions belonging to this service, in declaration order.</summary>
    public List<ProtoRpcDef> Methods { get; init; } = [];
}

/// <summary>
/// Represents an RPC method inside a gRPC service definition.
/// </summary>
public sealed class ProtoRpcDef
{
    /// <summary>RPC method name (PascalCase, e.g. <c>CreateOrder</c>).</summary>
    public string Name { get; init; } = "";

    /// <summary>The gRPC streaming pattern for this method (unary, server-streaming, etc.).</summary>
    public ProtoMethodType MethodType { get; init; }

    /// <summary>Proto message name for the request type.</summary>
    public string RequestTypeName { get; init; } = "";

    /// <summary>Proto message name for the response type.</summary>
    public string ResponseTypeName { get; init; } = "";

    /// <summary>
    /// When <see langword="true"/> emits <c>option deprecated = true;</c> on the rpc statement.
    /// </summary>
    public bool IsDeprecated { get; init; }

    /// <summary>Optional comment placed above the rpc statement.</summary>
    public string? Comment { get; init; }
}
