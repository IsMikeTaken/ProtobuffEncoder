using ProtobuffEncoder.Attributes;

[ProtoContract]
public class ChatReply
{
    [ProtoField(1)] public string Text { get; set; } = "";
    [ProtoField(2)] public bool IsSystem { get; set; }
}