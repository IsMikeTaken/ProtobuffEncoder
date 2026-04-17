namespace ProtobuffEncoder.Schema;

/// <summary>
/// A single value inside a proto enum definition.
/// </summary>
/// <param name="Name">Proto name in <c>SCREAMING_SNAKE_CASE</c> with the enum prefix applied.</param>
/// <param name="Number">The proto field number (≥ 0; 0 is reserved for the UNSPECIFIED sentinel).</param>
/// <param name="IsDeprecated">
/// When <see langword="true"/> the renderer emits <c>[deprecated = true]</c> after the value.
/// </param>
public sealed record ProtoEnumValue(
    string Name,
    int    Number,
    bool   IsDeprecated = false)
{
    // Parameterless constructor for object-initializer syntax compatibility.
    public ProtoEnumValue() : this("", 0) { }
}
