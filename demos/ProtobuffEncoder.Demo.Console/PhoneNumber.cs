using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Console;

[ProtoContract]
public class PhoneNumber
{
    public string Number { get; set; } = "";
    public ContactType Type { get; set; }
}