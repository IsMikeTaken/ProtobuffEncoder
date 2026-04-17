namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a <c>oneof</c> definition inside a proto message.
/// </summary>
public sealed class ProtoOneOfDef
{
    /// <summary>The oneof group name (snake_case in proto3).</summary>
    public string Name { get; init; } = "";

    /// <summary>The fields that belong to this oneof group.</summary>
    public List<ProtoFieldDef> Fields { get; init; } = [];
}
