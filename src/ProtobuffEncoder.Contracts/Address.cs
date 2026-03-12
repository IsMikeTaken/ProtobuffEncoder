using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts;

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