using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Console;

[ProtoContract]
public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";

    [ProtoField(FieldNumber = 10)]
    public int ZipCode { get; set; }
}