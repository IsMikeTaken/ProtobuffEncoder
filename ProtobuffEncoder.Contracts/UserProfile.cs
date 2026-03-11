using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts;

/// <summary>
/// Demonstrates deep nesting, maps, oneof, inheritance, and all new attribute features.
/// </summary>
[ProtoContract(IncludeBaseFields = true)]
[ProtoInclude(20, typeof(AdminProfile))]
[ProtoInclude(21, typeof(GuestProfile))]
public class UserProfile
{
    public string DisplayName { get; set; } = "";

    [ProtoField(IsRequired = true)]
    public string Email { get; set; } = "";

    public int Age { get; set; }

    /// <summary>
    /// Nested address — deep nesting with its own contract.
    /// </summary>
    public Address? PrimaryAddress { get; set; }

    /// <summary>
    /// Map of string → string: user preferences (e.g. "theme" → "dark").
    /// </summary>
    [ProtoMap]
    public Dictionary<string, string> Preferences { get; set; } = new();

    /// <summary>
    /// Map of string → nested message: named addresses (e.g. "home" → Address).
    /// </summary>
    [ProtoMap]
    public Dictionary<string, Address> Addresses { get; set; } = new();

    /// <summary>
    /// OneOf: the user's primary contact method — only one should be set.
    /// </summary>
    [ProtoOneOf("primary_contact")]
    public string? PhoneNumber { get; set; }

    [ProtoOneOf("primary_contact")]
    public string? SlackHandle { get; set; }

    public List<string> Tags { get; set; } = [];

    [ProtoField(IsDeprecated = true)]
    public string LegacyId { get; set; } = "";
}

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

/// <summary>
/// Derived type — guest-specific fields.
/// </summary>
[ProtoContract(IncludeBaseFields = true)]
public class GuestProfile : UserProfile
{
    public string InvitedBy { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}

/// <summary>
/// Deeply nested address type — itself has a nested GeoCoordinate.
/// </summary>
[ProtoContract]
public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public string PostalCode { get; set; } = "";

    /// <summary>
    /// Second-level nesting: coordinates within an address.
    /// </summary>
    public GeoCoordinate? Location { get; set; }
}

/// <summary>
/// Third-level deep nesting: GPS coordinates.
/// </summary>
[ProtoContract]
public class GeoCoordinate
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Altitude { get; set; }
}
