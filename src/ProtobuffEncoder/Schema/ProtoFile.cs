namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a parsed .proto file schema.
/// </summary>
public sealed class ProtoFile
{
    public string Syntax { get; set; } = "proto3";
    public string Package { get; set; } = "";
    public List<ProtoMessageDef> Messages { get; set; } = [];
    public List<ProtoEnumDef> Enums { get; set; } = [];
}