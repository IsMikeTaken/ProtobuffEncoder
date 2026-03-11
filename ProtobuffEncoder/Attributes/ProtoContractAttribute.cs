namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Marks a class for protobuf serialization. Properties are included by default
/// with auto-assigned field numbers based on declaration order.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
public sealed class ProtoContractAttribute : Attribute
{
    /// <summary>
    /// When true, only properties explicitly marked with <see cref="ProtoFieldAttribute"/> are included.
    /// When false (default), all public properties are included automatically.
    /// </summary>
    public bool ExplicitFields { get; set; }

    /// <summary>
    /// When true, base class properties are included in the serialization (walked up the inheritance chain).
    /// Default is false — only the declaring type's own properties are serialized.
    /// </summary>
    public bool IncludeBaseFields { get; set; }

    /// <summary>
    /// When true, nested object properties whose types lack [ProtoContract] are
    /// implicitly treated as contracts and auto-serialized. This allows deep nesting
    /// without requiring every nested class to be explicitly attributed.
    /// </summary>
    public bool ImplicitFields { get; set; }

    /// <summary>
    /// When true, fields holding their type's default value are skipped globally for this message.
    /// Individual fields can still override via <see cref="ProtoFieldAttribute.WriteDefault"/>.
    /// Default is true (proto3 behaviour).
    /// </summary>
    public bool SkipDefaults { get; set; } = true;
}
