namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Overrides the proto field number and/or the rendered name for a single enum member.
/// </summary>
/// <remarks>
/// <para>
/// Without this attribute the generator assigns field numbers sequentially and derives the
/// proto name from the CLR member name using <c>SCREAMING_SNAKE_CASE</c> with the enum
/// prefix applied.
/// </para>
/// <para>
/// Explicit field-number assignment is required when:
/// <list type="bullet">
///   <item>The CLR enum has non-sequential or non-zero-based numeric values.</item>
///   <item>You need a value at 0 that already has a meaningful name (bypassing the
///         auto-generated <c>UNSPECIFIED</c> sentinel).</item>
///   <item>Two members share the same proto number (alias — also set
///         <see cref="ProtoEnumAttribute.AllowAlias"/> on the containing enum).</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public enum Direction
/// {
///     [ProtoEnumValue(0, "DIRECTION_UNSPECIFIED")] None  = 0,
///     [ProtoEnumValue(1, "DIRECTION_NORTH")]       North = 1,
///     [ProtoEnumValue(2, "DIRECTION_SOUTH")]       South = 2,
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ProtoEnumValueAttribute : Attribute
{
    /// <summary>
    /// The proto field number for this enum value.
    /// When <see langword="null"/> the CLR numeric value is used.
    /// </summary>
    public int? Number { get; }

    /// <summary>
    /// The exact proto name to emit for this value.
    /// When <see langword="null"/> the name is derived automatically.
    /// </summary>
    public string? Name { get; }

    /// <param name="number">Proto field number override.</param>
    public ProtoEnumValueAttribute(int number)
    {
        Number = number;
        Name = null;
    }

    /// <param name="number">Proto field number override.</param>
    /// <param name="name">Explicit proto name (must follow <c>SCREAMING_SNAKE_CASE</c> convention).</param>
    public ProtoEnumValueAttribute(int number, string name)
    {
        Number = number;
        Name = name;
    }
}
