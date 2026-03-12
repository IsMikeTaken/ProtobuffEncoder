namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Marks a Dictionary&lt;TKey, TValue&gt; property as a protobuf map field.
/// In the proto3 schema this generates: <c>map&lt;key_type, value_type&gt; field_name = N;</c>
/// Without this attribute, dictionaries are not serialized.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ProtoMapAttribute : Attribute
{
    /// <summary>
    /// Optional override for the key's proto type name (e.g. "string", "int32").
    /// When null, inferred from the CLR dictionary key type.
    /// </summary>
    public string? KeyType { get; set; }

    /// <summary>
    /// Optional override for the value's proto type name (e.g. "string", "int32", or a message name).
    /// When null, inferred from the CLR dictionary value type.
    /// </summary>
    public string? ValueType { get; set; }
}
