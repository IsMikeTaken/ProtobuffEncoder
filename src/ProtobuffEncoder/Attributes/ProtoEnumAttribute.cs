namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Controls how a CLR enum is rendered in the generated <c>.proto</c> file.
/// </summary>
/// <remarks>
/// <para>
/// By default the schema generator applies the proto3 best-practice rules automatically:
/// <list type="bullet">
///   <item>Values are rendered in <c>SCREAMING_SNAKE_CASE</c>.</item>
///   <item>A zero-value sentinel <c>FOO_UNSPECIFIED = 0;</c> is synthesised when the enum
///         has no explicit zero entry.</item>
///   <item>All value names are prefixed with <c>ENUM_NAME_</c> to avoid proto-level naming
///         collisions across packages.</item>
/// </list>
/// Use <see cref="ProtoEnumAttribute"/> to override any of those defaults.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ProtoEnum(Prefix = "STATUS", AllowAlias = true)]
/// public enum OrderStatus
/// {
///     [ProtoEnumValue(0, "STATUS_UNSPECIFIED")] Unknown  = 0,
///     [ProtoEnumValue(1)]                       Pending  = 1,
///     [ProtoEnumValue(2)]                       Accepted = 2,
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class ProtoEnumAttribute : Attribute
{
    /// <summary>
    /// Explicit prefix used for all value names in the generated <c>.proto</c>.
    /// When <see langword="null"/> the prefix is derived from the enum name in
    /// <c>SCREAMING_SNAKE_CASE</c> (e.g. <c>OrderStatus</c> → <c>ORDER_STATUS</c>).
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// When <see langword="true"/> the generator emits <c>option allow_alias = true;</c>,
    /// which permits multiple values to share the same field number.  Required when two
    /// C# enum members map to the same proto number.
    /// </summary>
    public bool AllowAlias { get; set; }

    /// <summary>
    /// Suppresses the automatic insertion of the <c>UNSPECIFIED = 0</c> sentinel value.
    /// Use only when the enum already declares an explicit zero-value entry (either via
    /// <see cref="ProtoEnumValueAttribute"/> or the CLR default).
    /// </summary>
    public bool SuppressUnspecified { get; set; }

    /// <summary>
    /// Optional human-readable comment placed above the generated enum definition.
    /// </summary>
    public string? Comment { get; set; }
}
