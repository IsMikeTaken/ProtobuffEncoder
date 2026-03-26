using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Template.Normal.Contracts;

[ProtoContract]
public class ChatReply
{
    [ProtoField(1)] public string Text { get; set; } = "";
    [ProtoField(2)] public bool IsSystem { get; set; }
}
