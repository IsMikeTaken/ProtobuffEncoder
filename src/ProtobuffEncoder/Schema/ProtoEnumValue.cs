namespace ProtobuffEncoder.Schema;

/// <summary>
/// A single value inside a proto enum.
/// </summary>
public sealed class ProtoEnumValue
{
    public string Name { get; set; } = "";
    public int Number { get; set; }
}