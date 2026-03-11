using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Console;

public enum ContactType
{
    Unknown = 0,
    Personal = 1,
    Work = 2
}

[ProtoContract]
public class Person
{
    public string Name { get; set; } = "";

    [ProtoField(FieldNumber = 5, Name = "email_address")]
    public string Email { get; set; } = "";

    public int Age { get; set; }

    [ProtoIgnore]
    public string InternalNotes { get; set; } = "";

    // Nested message
    public Address? HomeAddress { get; set; }

    // Nullable value type
    public double? Score { get; set; }

    // Enum
    public ContactType Type { get; set; }

    // Collections
    public List<string> Tags { get; set; } = [];
    public int[] LuckyNumbers { get; set; } = [];

    // Collection of nested messages
    public List<PhoneNumber> PhoneNumbers { get; set; } = [];
}

[ProtoContract]
public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";

    [ProtoField(FieldNumber = 10)]
    public int ZipCode { get; set; }
}

[ProtoContract]
public class PhoneNumber
{
    public string Number { get; set; } = "";
    public ContactType Type { get; set; }
}
