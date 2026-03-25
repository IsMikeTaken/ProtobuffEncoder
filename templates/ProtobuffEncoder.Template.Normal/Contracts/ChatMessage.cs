using ProtobuffEncoder.Attributes;

[ProtoContract(DefaultEncoding = "utf-8")]
public class ChatMessage
{
    [ProtoField(1)] public string Author { get; set; } = "";
    [ProtoField(2)] public string Text { get; set; } = "";
}