using System.Reflection;

namespace ProtobuffEncoder;

/// <summary>
/// Resolved metadata for a single protobuf field, combining reflection info with attribute overrides.
/// </summary>
internal sealed class FieldDescriptor
{
    public required int FieldNumber { get; init; }
    public required string Name { get; init; }
    public required WireType WireType { get; init; }
    public required PropertyInfo Property { get; init; }
    public required bool WriteDefault { get; init; }

    /// <summary>
    /// True when the property is a repeated field (array, List, ICollection, etc.).
    /// </summary>
    public required bool IsCollection { get; init; }

    /// <summary>
    /// The element type for collections, or null for scalar/message fields.
    /// </summary>
    public Type? ElementType { get; init; }

    /// <summary>
    /// The wire type of individual elements inside a collection.
    /// Only meaningful when <see cref="IsCollection"/> is true.
    /// </summary>
    public WireType ElementWireType { get; init; }

    /// <summary>
    /// True when the property type is Nullable&lt;T&gt;.
    /// </summary>
    public required bool IsNullable { get; init; }

    /// <summary>
    /// True when the property is a Dictionary&lt;TKey, TValue&gt; (proto map field).
    /// </summary>
    public bool IsMap { get; init; }

    /// <summary>
    /// The dictionary key type. Only set when <see cref="IsMap"/> is true.
    /// </summary>
    public Type? MapKeyType { get; init; }

    /// <summary>
    /// The dictionary value type. Only set when <see cref="IsMap"/> is true.
    /// </summary>
    public Type? MapValueType { get; init; }

    /// <summary>
    /// The wire type of dictionary keys. Only meaningful when <see cref="IsMap"/> is true.
    /// </summary>
    public WireType MapKeyWireType { get; init; }

    /// <summary>
    /// The wire type of dictionary values. Only meaningful when <see cref="IsMap"/> is true.
    /// </summary>
    public WireType MapValueWireType { get; init; }

    /// <summary>
    /// The oneof group name, or null when the field is not part of a oneof.
    /// </summary>
    public string? OneOfGroup { get; init; }

    /// <summary>
    /// Explicit packed-encoding override. Null means auto (packed for packable scalars).
    /// </summary>
    public bool? IsPacked { get; init; }

    /// <summary>
    /// Whether the field is deprecated in the schema.
    /// </summary>
    public bool IsDeprecated { get; init; }

    /// <summary>
    /// Whether the field is required (validated at encode time).
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// True when the field type was implicitly resolved as a contract
    /// (the type itself doesn't have [ProtoContract] but the parent set ImplicitFields = true).
    /// </summary>
    public bool IsImplicit { get; init; }

    /// <summary>
    /// The text encoding to use for string fields. Resolved from field-level or contract-level
    /// encoding attributes. Null means UTF-8 (the protobuf default).
    /// </summary>
    public ProtoEncoding? Encoding { get; init; }
}
