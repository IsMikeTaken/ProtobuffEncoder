using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts;

/// <summary>
/// Derived type — guest-specific fields.
/// </summary>
[ProtoContract(IncludeBaseFields = true)]
public class GuestProfile : UserProfile
{
    public string InvitedBy { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}