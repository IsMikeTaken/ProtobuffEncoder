using System.Reflection;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a gRPC service definition in a .proto file.
/// </summary>
public sealed class ProtoServiceDef
{
    /// <summary>Service name (PascalCase, e.g. <c>OrderService</c>).</summary>
    public string Name { get; set; } = "";

    /// <summary>The originating CLR interface type, used for traceability comments.</summary>
    public Type? SourceType { get; set; }

    /// <summary>Optional human-readable comment placed above the service block.</summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// When <see langword="true"/> emits <c>option deprecated = true;</c> inside the service block.
    /// </summary>
    public bool IsDeprecated { get; set; }

    public List<ProtoRpcDef> Methods { get; set; } = [];
}

/// <summary>
/// Represents an RPC method inside a gRPC service definition.
/// </summary>
public sealed class ProtoRpcDef
{
    /// <summary>RPC method name (PascalCase, e.g. <c>CreateOrder</c>).</summary>
    public string Name { get; set; } = "";

    public ProtoMethodType MethodType { get; set; }

    /// <summary>Proto message name for the request type.</summary>
    public string RequestTypeName { get; set; } = "";

    /// <summary>Proto message name for the response type.</summary>
    public string ResponseTypeName { get; set; } = "";

    /// <summary>
    /// When <see langword="true"/> emits <c>option deprecated = true;</c> on the rpc statement.
    /// </summary>
    public bool IsDeprecated { get; set; }

    /// <summary>Optional comment placed above the rpc statement.</summary>
    public string? Comment { get; set; }
}
