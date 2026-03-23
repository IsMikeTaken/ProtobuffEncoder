namespace ProtobuffEncoder;

/// <summary>
/// Determines how field numbers are auto-assigned to properties that don't have
/// an explicit <c>[ProtoField(N)]</c> field number.
/// </summary>
public enum FieldNumbering
{
    /// <summary>
    /// Assigns field numbers in property declaration order (1, 2, 3, ...).
    /// This is the default behavior and matches protobuf convention.
    /// </summary>
    DeclarationOrder = 0,

    /// <summary>
    /// Assigns field numbers alphabetically by property name.
    /// Produces deterministic numbering regardless of source code ordering.
    /// </summary>
    Alphabetical = 1,

    /// <summary>
    /// Groups properties by type category, then sorts alphabetically within each group.
    /// Order: scalars (bool, int, string, etc.) → collections → nested messages.
    /// Useful for logical grouping in generated schemas.
    /// </summary>
    TypeThenAlphabetical = 2
}
