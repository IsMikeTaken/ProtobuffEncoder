using System.ComponentModel;

namespace ProtobuffEncoder;

/// <summary>
/// Defines the protobuf wire types used for encoding data.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum WireType
{
    Varint = 0,
    Fixed64 = 1,
    LengthDelimited = 2,
    Fixed32 = 5
}
