using System.Reflection;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a gRPC service definition in a .proto file.
/// </summary>
public sealed class ProtoServiceDef
{
    public string Name { get; set; } = "";
    public Type? SourceType { get; set; }
    public string? Metadata { get; set; }
    public List<ProtoRpcDef> Methods { get; set; } = [];
}

/// <summary>
/// Represents an RPC method inside a gRPC service definition.
/// </summary>
public sealed class ProtoRpcDef
{
    public string Name { get; set; } = "";
    public ProtoMethodType MethodType { get; set; }
    public string RequestTypeName { get; set; } = "";
    public string ResponseTypeName { get; set; } = "";
}
