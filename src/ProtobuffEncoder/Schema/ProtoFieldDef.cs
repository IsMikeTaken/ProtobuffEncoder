namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a field definition in a proto message.
/// </summary>
public sealed class ProtoFieldDef
{
    /// <summary>Proto field name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Proto field number (1-based).</summary>
    public int FieldNumber { get; init; }

    /// <summary>Proto type name (e.g. <c>string</c>, <c>int32</c>, <c>MyMessage</c>).</summary>
    public string TypeName { get; init; } = "";

    /// <summary>When <see langword="true"/> the field is a repeated (list) field.</summary>
    public bool IsRepeated { get; init; }

    /// <summary>When <see langword="true"/> the field is wrapped in <c>optional</c>.</summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// When <see langword="true"/> the renderer emits <c>[deprecated = true]</c>.
    /// </summary>
    public bool IsDeprecated { get; init; }

    /// <summary>
    /// When <see langword="true"/> this is a <c>map&lt;K, V&gt;</c> field.
    /// </summary>
    public bool IsMap { get; init; }

    /// <summary>
    /// The map key proto type (e.g. <c>"string"</c>, <c>"int32"</c>).
    /// Only set when <see cref="IsMap"/> is <see langword="true"/>.
    /// </summary>
    public string MapKeyType { get; init; } = "";

    /// <summary>
    /// The map value proto type.
    /// Only set when <see cref="IsMap"/> is <see langword="true"/>.
    /// </summary>
    public string MapValueType { get; init; } = "";

    /// <summary>
    /// The oneof group this field belongs to, or <see langword="null"/>.
    /// </summary>
    public string? OneOfGroup { get; set; }
}
