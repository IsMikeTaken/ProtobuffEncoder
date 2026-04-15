namespace ProtobuffEncoder.Schema;

/// <summary>
/// A single value inside a proto enum definition.
/// </summary>
public sealed class ProtoEnumValue
{
    /// <summary>Proto name in <c>SCREAMING_SNAKE_CASE</c> with the enum prefix applied.</summary>
    public string Name { get; set; } = "";

    /// <summary>The proto field number (≥ 0; 0 is reserved for the UNSPECIFIED sentinel).</summary>
    public int Number { get; set; }

    /// <summary>
    /// When <see langword="true"/> the renderer emits <c>[deprecated = true]</c> after the value.
    /// </summary>
    public bool IsDeprecated { get; set; }
}
