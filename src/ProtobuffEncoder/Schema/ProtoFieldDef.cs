namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a field definition in a proto message.
/// </summary>
public sealed class ProtoFieldDef
{
    public string Name { get; set; } = "";
    public int FieldNumber { get; set; }
    public string TypeName { get; set; } = "";
    public bool IsRepeated { get; set; }
    public bool IsOptional { get; set; }
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// True when this field is a map field (map&lt;K, V&gt;).
    /// </summary>
    public bool IsMap { get; set; }

    /// <summary>
    /// The map key proto type (e.g. "string", "int32"). Only set when <see cref="IsMap"/> is true.
    /// </summary>
    public string MapKeyType { get; set; } = "";

    /// <summary>
    /// The map value proto type. Only set when <see cref="IsMap"/> is true.
    /// </summary>
    public string MapValueType { get; set; } = "";

    /// <summary>
    /// The oneof group this field belongs to, or null.
    /// </summary>
    public string? OneOfGroup { get; set; }
}