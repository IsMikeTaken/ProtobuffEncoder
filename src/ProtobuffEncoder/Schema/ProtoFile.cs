namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a parsed .proto file schema.
/// </summary>
public sealed class ProtoFile
{
    public string Syntax { get; set; } = "proto3";
    public string Package { get; set; } = "";

    /// <summary>
    /// The file path key (e.g. "v1/Order.proto") used during multi-file generation.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Import statements referencing other .proto files that define types used in this file.
    /// </summary>
    public List<string> Imports { get; set; } = [];

    public List<ProtoMessageDef> Messages { get; set; } = [];
    public List<ProtoEnumDef> Enums { get; set; } = [];
    public List<ProtoServiceDef> Services { get; set; } = [];
}