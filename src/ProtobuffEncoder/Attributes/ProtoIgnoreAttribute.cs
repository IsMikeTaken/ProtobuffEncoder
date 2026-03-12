namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Excludes a property from protobuf serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ProtoIgnoreAttribute : Attribute;
