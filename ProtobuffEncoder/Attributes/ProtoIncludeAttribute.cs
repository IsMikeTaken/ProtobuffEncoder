namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Declares a known derived type on a base class, enabling polymorphic serialization.
/// Each derived type is encoded as a nested message at the specified field number.
/// Apply multiple times to register multiple subtypes.
/// <example>
/// [ProtoContract]
/// [ProtoInclude(10, typeof(Dog))]
/// [ProtoInclude(11, typeof(Cat))]
/// public class Animal { public string Name { get; set; } }
/// </example>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ProtoIncludeAttribute : Attribute
{
    /// <summary>
    /// The field number used for the derived type's nested message.
    /// Must be unique within the base type and must not collide with the base type's own field numbers.
    /// </summary>
    public int FieldNumber { get; }

    /// <summary>
    /// The derived CLR type.
    /// </summary>
    public Type DerivedType { get; }

    public ProtoIncludeAttribute(int fieldNumber, Type derivedType)
    {
        FieldNumber = fieldNumber;
        DerivedType = derivedType;
    }
}
