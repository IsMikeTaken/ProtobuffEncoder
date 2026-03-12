namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a message definition in a .proto file.
/// </summary>
public sealed class ProtoMessageDef
{
    public string Name { get; set; } = "";
    public List<ProtoFieldDef> Fields { get; set; } = [];
    public List<ProtoMessageDef> NestedMessages { get; set; } = [];
    public List<ProtoEnumDef> NestedEnums { get; set; } = [];
    public List<ProtoOneOfDef> OneOfs { get; set; } = [];
}