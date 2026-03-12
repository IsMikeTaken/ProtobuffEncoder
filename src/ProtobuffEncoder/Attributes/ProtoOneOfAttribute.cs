namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Groups properties into a protobuf <c>oneof</c> union. At most one property in a
/// oneof group should have a non-default value. During encoding, only the first
/// non-default property in the group is written.
/// <example>
/// [ProtoOneOf("contact")]
/// public string? Email { get; set; }
///
/// [ProtoOneOf("contact")]
/// public string? Phone { get; set; }
/// </example>
/// Generates: <c>oneof contact { string Email = N; string Phone = M; }</c>
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ProtoOneOfAttribute : Attribute
{
    /// <summary>
    /// The name of the oneof group. All properties sharing the same group name
    /// are rendered inside a single <c>oneof { }</c> block in the .proto schema.
    /// </summary>
    public string GroupName { get; }

    public ProtoOneOfAttribute(string groupName)
    {
        GroupName = groupName;
    }
}
