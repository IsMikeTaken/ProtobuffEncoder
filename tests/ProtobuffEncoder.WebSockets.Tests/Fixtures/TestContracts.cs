using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.WebSockets.Tests.Fixtures;

/// <summary>Shared test message contracts used across all test classes.</summary>

[ProtoContract]
public class Heartbeat
{
    [ProtoField(1)] public long Timestamp { get; set; }
}

[ProtoContract]
public class ChatMessage
{
    [ProtoField(1)] public string User { get; set; } = "";
    [ProtoField(2)] public string Text { get; set; } = "";
    [ProtoField(3)] public long SentAt { get; set; }
}

[ProtoContract]
public class ChatReply
{
    [ProtoField(1)] public string From { get; set; } = "";
    [ProtoField(2)] public string Body { get; set; } = "";
    [ProtoField(3)] public bool Delivered { get; set; }
}

[ProtoContract]
public class LargePayload
{
    [ProtoField(1)] public string Data { get; set; } = "";
    [ProtoField(2)] public int SequenceNumber { get; set; }
}

[ProtoContract]
public class EmptyMessage { }
