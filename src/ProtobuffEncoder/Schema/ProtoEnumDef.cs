namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents an enum definition in a .proto file.
/// </summary>
public sealed class ProtoEnumDef
{
    public string Name { get; set; } = "";
    public List<ProtoEnumValue> Values { get; set; } = [];
}