namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents an enum definition in a .proto file, including proto3 best-practice metadata.
/// </summary>
public sealed class ProtoEnumDef
{
    /// <summary>Proto enum name (PascalCase, e.g. <c>OrderStatus</c>).</summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// When <see langword="true"/> the renderer emits <c>option allow_alias = true;</c>.
    /// </summary>
    public bool AllowAlias { get; init; }

    /// <summary>Optional comment placed above the enum block.</summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Field numbers that have been removed and must not be reused.
    /// Renders as <c>reserved 1, 2, 3;</c>.
    /// </summary>
    public IReadOnlyList<int> ReservedNumbers { get; init; } = [];

    /// <summary>
    /// Value names that have been removed and must not be reused.
    /// Renders as <c>reserved "OLD_NAME_FOO";</c>.
    /// </summary>
    public IReadOnlyList<string> ReservedNames { get; init; } = [];

    /// <summary>All enum value definitions, ordered by number ascending.</summary>
    public List<ProtoEnumValue> Values { get; init; } = [];
}
