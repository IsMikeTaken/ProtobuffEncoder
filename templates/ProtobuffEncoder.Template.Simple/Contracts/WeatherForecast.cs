using ProtobuffEncoder.Attributes;

[ProtoContract]
public class WeatherForecast
{
    [ProtoField(1)] public string City { get; set; } = "";
    [ProtoField(2)] public List<DayEntry> Entries { get; set; } = [];
}