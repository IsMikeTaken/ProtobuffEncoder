using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts;

/// <summary>
/// Derived type — admin-specific fields serialized via ProtoInclude.
/// </summary>
[ProtoContract(IncludeBaseFields = true)]
public class AdminProfile : UserProfile
{
    public string Department { get; set; } = "";

    [ProtoField(FieldNumber = 1)]
    public int PermissionLevel { get; set; }
}