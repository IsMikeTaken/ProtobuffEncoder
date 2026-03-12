using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts;

public enum NotificationLevel
{
    Info = 0,
    Warning = 1,
    Error = 2
}

[ProtoContract]
public class NotificationMessage
{
    public string Source { get; set; } = "";
    public string Text { get; set; } = "";
    public NotificationLevel Level { get; set; }
    public long TimestampUtc { get; set; }
    public List<string> Tags { get; set; } = [];
}

[ProtoContract]
public class AckResponse
{
    public bool Accepted { get; set; }
    public string MessageId { get; set; } = "";
}
