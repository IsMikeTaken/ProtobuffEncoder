namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents an enum definition in a .proto file, including proto3 best-practice metadata.
/// </summary>
public sealed class ProtoEnumDef
{
    /// <summary>Proto enum name (PascalCase, e.g. <c>OrderStatus</c>).</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// When <see langword="true"/> the renderer emits <c>option allow_alias = true;</c>.
    /// </summary>
    public bool AllowAlias { get; set; }

    /// <summary>Optional comment placed above the enum block.</summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Field numbers that have been removed and must not be reused.
    /// Renders as <c>reserved 1, 2, 3;</c>.
    /// </summary>
    public List<int> ReservedNumbers { get; set; } = [];

    /// <summary>
    /// Value names that have been removed and must not be reused.
    /// Renders as <c>reserved "OLD_NAME_FOO";</c>.
    /// </summary>
    public List<string> ReservedNames { get; set; } = [];

    /// <summary>All enum value definitions, ordered by number ascending.</summary>
    public List<ProtoEnumValue> Values { get; set; } = [];
}