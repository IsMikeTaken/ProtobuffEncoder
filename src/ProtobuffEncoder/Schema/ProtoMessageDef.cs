namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a message definition in a .proto file.
/// </summary>
public sealed class ProtoMessageDef
{
    /// <summary>Message name (PascalCase, no package prefix).</summary>
    public string Name { get; init; } = "";

    /// <summary>The originating CLR type, used for traceability comments.</summary>
    public Type? SourceType { get; init; }

    /// <summary>Optional human-readable comment placed above the message block.</summary>
    public string? Metadata { get; init; }

    /// <summary>
    /// Field numbers that have been removed and must not be reused.
    /// Renders as <c>reserved 2, 15, 9 to 11;</c>.
    /// </summary>
    public IReadOnlyList<int> ReservedNumbers { get; init; } = [];

    /// <summary>
    /// Field names that have been removed and must not be reused.
    /// Renders as <c>reserved "foo", "bar";</c>.
    /// </summary>
    public IReadOnlyList<string> ReservedNames { get; init; } = [];

    /// <summary>Top-level field definitions of this message.</summary>
    public List<ProtoFieldDef> Fields { get; init; } = [];

    /// <summary>Message types nested inside this message.</summary>
    public List<ProtoMessageDef> NestedMessages { get; init; } = [];

    /// <summary>Enum types nested inside this message.</summary>
    public List<ProtoEnumDef> NestedEnums { get; init; } = [];

    /// <summary>Oneof groups defined inside this message.</summary>
    public List<ProtoOneOfDef> OneOfs { get; init; } = [];
}
