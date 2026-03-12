using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Contracts;

[ProtoContract]
public class AckResponse
{
    public bool Accepted { get; set; }
    public string MessageId { get; set; } = "";
}