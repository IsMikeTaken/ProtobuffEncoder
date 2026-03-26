using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Template.Normal.Contracts;

[ProtoContract]
public class Team
{
    [ProtoField(1)] public string Name { get; set; } = "";
    [ProtoField(2)] public List<string> Members { get; set; } = [];

    [ProtoMap]
    [ProtoField(3)] public Dictionary<string, int> Scores { get; set; } = new();
}
