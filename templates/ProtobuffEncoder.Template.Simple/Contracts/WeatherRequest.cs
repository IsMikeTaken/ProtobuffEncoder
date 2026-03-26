using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Template.Simple.Contracts;

[ProtoContract]
public class WeatherRequest
{
    [ProtoField(1)] public string City { get; set; } = "";
    [ProtoField(2)] public int Days { get; set; }
    [ProtoField(3)] public bool IncludeWind { get; set; }
}
