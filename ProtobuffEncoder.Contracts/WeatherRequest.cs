using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts;

[ProtoContract]
public class WeatherRequest
{
    public string City { get; set; } = "";
    public int Days { get; set; }
    public bool IncludeHourly { get; set; }
}
