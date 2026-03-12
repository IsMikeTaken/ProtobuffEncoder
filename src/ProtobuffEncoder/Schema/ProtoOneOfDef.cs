namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a oneof definition in a proto message.
/// </summary>
public sealed class ProtoOneOfDef
{
    public string Name { get; set; } = "";
    public List<ProtoFieldDef> Fields { get; set; } = [];
}