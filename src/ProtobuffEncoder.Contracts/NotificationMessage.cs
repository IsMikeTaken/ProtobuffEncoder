using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts;

[ProtoContract]
public class NotificationMessage
{
    public string Source { get; set; } = "";
    public string Text { get; set; } = "";
    public NotificationLevel Level { get; set; }
    public long TimestampUtc { get; set; }
    public List<string> Tags { get; set; } = [];
}