using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts;

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