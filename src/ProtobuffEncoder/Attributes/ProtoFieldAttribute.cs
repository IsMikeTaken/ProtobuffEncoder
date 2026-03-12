namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Overrides protobuf field metadata for a property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ProtoFieldAttribute : Attribute
{
    /// <summary>
    /// The protobuf field number (1-based). When 0, auto-assigned based on declaration order.
    /// </summary>
    public int FieldNumber { get; set; }

    /// <summary>
    /// Override the field name used in the protobuf schema. Defaults to the property name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Force a specific wire type. When null, inferred from the property's CLR type.
    /// </summary>
    public WireType? WireType { get; set; }

    /// <summary>
    /// When true, the field is written even if it holds the default value for its type.
    /// </summary>
    public bool WriteDefault { get; set; }

    /// <summary>
    /// Controls packed encoding for repeated scalar fields.
    /// When null (default), packed encoding is auto-detected (proto3 default: packed).
    /// Set to true/false to explicitly control packing.
    /// </summary>
    public bool? IsPacked { get; set; }

    /// <summary>
    /// Marks the field as deprecated in generated .proto schemas.
    /// The field is still serialized, but the schema annotation signals consumers should migrate.
    /// </summary>
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// Marks the field as required. When true, encoding throws if the value is null or default.
    /// Maps to a validation check — proto3 does not have a required keyword, but this
    /// enforces it at the library level.
    /// </summary>
    public bool IsRequired { get; set; }
}
