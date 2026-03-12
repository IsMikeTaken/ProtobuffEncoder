using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts;

[ProtoContract]
public class DayForecast
{
    public string Date { get; set; } = "";
    public double TemperatureMin { get; set; }
    public double TemperatureMax { get; set; }
    public string Condition { get; set; } = "";
    public int HumidityPercent { get; set; }
    public double? WindSpeed { get; set; }
}