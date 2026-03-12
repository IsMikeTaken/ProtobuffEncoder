namespace ProtobuffEncoder;

/// <summary>
/// Protobuf wire types as defined in the protocol buffer encoding specification.
/// </summary>
public enum WireType
{
    Varint = 0,
    Fixed64 = 1,
    LengthDelimited = 2,
    Fixed32 = 5
}
