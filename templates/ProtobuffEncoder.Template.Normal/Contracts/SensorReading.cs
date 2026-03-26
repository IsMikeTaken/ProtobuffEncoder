using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Template.Normal.Contracts;

[ProtoContract]
public class SensorReading
{
    [ProtoField(1, IsRequired = true)]
    public string SensorId { get; set; } = "";

    [ProtoField(2)] public double Value { get; set; }
    [ProtoField(3)] public DateTime Timestamp { get; set; }
    [ProtoField(4)] public double? ErrorMargin { get; set; }
}
