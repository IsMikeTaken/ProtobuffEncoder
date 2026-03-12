using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts;

[ProtoContract]
public class WeatherResponse
{
    public string City { get; set; } = "";
    public List<DayForecast> Forecasts { get; set; } = [];
    public long GeneratedAtUtc { get; set; }
}