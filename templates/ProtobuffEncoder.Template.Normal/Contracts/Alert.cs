using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Template.Normal.Contracts;

[ProtoContract]
public class Alert
{
    [ProtoField(1)] public int Id { get; set; }
    [ProtoField(2)] public string Text { get; set; } = "";

    [ProtoOneOf("channel")]
    [ProtoField(3)] public string? Email { get; set; }

    [ProtoOneOf("channel")]
    [ProtoField(4)] public string? Sms { get; set; }

    [ProtoOneOf("channel")]
    [ProtoField(5)] public string? Push { get; set; }
}
