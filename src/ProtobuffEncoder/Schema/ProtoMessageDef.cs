namespace ProtobuffEncoder.Schema;

/// <summary>
/// Represents a message definition in a .proto file.
/// </summary>
public sealed class ProtoMessageDef
{
    /// <summary>Message name (PascalCase, no package prefix).</summary>
    public string Name { get; set; } = "";

    /// <summary>The originating CLR type, used for traceability comments.</summary>
    public Type? SourceType { get; set; }

    /// <summary>Optional human-readable comment placed above the message block.</summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Field numbers that have been removed and must not be reused.
    /// Renders as <c>reserved 2, 15, 9 to 11;</c>.
    /// </summary>
    public List<int> ReservedNumbers { get; set; } = [];

    /// <summary>
    /// Field names that have been removed and must not be reused.
    /// Renders as <c>reserved "foo", "bar";</c>.
    /// </summary>
    public List<string> ReservedNames { get; set; } = [];

    public List<ProtoFieldDef> Fields { get; set; } = [];
    public List<ProtoMessageDef> NestedMessages { get; set; } = [];
    public List<ProtoEnumDef> NestedEnums { get; set; } = [];
    public List<ProtoOneOfDef> OneOfs { get; set; } = [];
}
