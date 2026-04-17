namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a parsed or generated .proto file schema.
/// </summary>
public sealed class ProtoFile
{
    /// <summary>The proto syntax version. Always <c>"proto3"</c> for this library.</summary>
    public string Syntax { get; set; } = "proto3";

    /// <summary>The proto package name (e.g. <c>"com.example.v1"</c>). Empty when unset.</summary>
    public string Package { get; set; } = "";

    /// <summary>
    /// The file path key (e.g. <c>"v1/Order.proto"</c>) used during multi-file generation.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Import statements referencing other .proto files that define types used in this file.
    /// Populated by cross-file import resolution in <c>ProtoSchemaGenerator</c>.
    /// </summary>
    public List<string> Imports { get; set; } = [];

    /// <summary>
    /// Well-known type import paths (e.g. <c>"google/protobuf/timestamp.proto"</c>) collected
    /// during model building. Merged ahead of <see cref="Imports"/> when rendering.
    /// </summary>
    public HashSet<string>? WellKnownImports { get; set; }

    /// <summary>Top-level message definitions declared in this file.</summary>
    public List<ProtoMessageDef> Messages { get; set; } = [];

    /// <summary>Top-level enum definitions declared in this file.</summary>
    public List<ProtoEnumDef> Enums { get; set; } = [];

    /// <summary>gRPC service definitions declared in this file.</summary>
    public List<ProtoServiceDef> Services { get; set; } = [];
}
