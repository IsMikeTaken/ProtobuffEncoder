using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Template.Simple.Contracts;

[ProtoContract]
public class DayEntry
{
    [ProtoField(1)] public string Date { get; set; } = "";
    [ProtoField(2)] public double HighC { get; set; }
    [ProtoField(3)] public double LowC { get; set; }
    [ProtoField(4)] public string Condition { get; set; } = "";
}
