using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Tests;

// ═══════════════════════════════════════════════════
// Shared test models used across multiple test files
// ═══════════════════════════════════════════════════

[ProtoContract]
public class SimpleMessage
{
    [ProtoField(1)]
    public int Id { get; set; }

    [ProtoField(2)]
    public string Name { get; set; } = "";

    [ProtoField(3)]
    public bool IsActive { get; set; }
}

[ProtoContract]
public class AllScalarsMessage
{
    [ProtoField(1)] public bool Flag { get; set; }
    [ProtoField(2)] public byte ByteValue { get; set; }
    [ProtoField(3)] public sbyte SByteValue { get; set; }
    [ProtoField(4)] public short ShortValue { get; set; }
    [ProtoField(5)] public ushort UShortValue { get; set; }
    [ProtoField(6)] public int IntValue { get; set; }
    [ProtoField(7)] public uint UIntValue { get; set; }
    [ProtoField(8)] public long LongValue { get; set; }
    [ProtoField(9)] public ulong ULongValue { get; set; }
    [ProtoField(10)] public float FloatValue { get; set; }
    [ProtoField(11)] public double DoubleValue { get; set; }
    [ProtoField(12)] public string StringValue { get; set; } = "";
    [ProtoField(13)] public byte[] ByteArrayValue { get; set; } = [];
}

public enum Priority { Low = 0, Medium = 1, High = 2, Critical = 3 }

[ProtoContract]
public class EnumMessage
{
    [ProtoField(1)] public Priority Priority { get; set; }
    [ProtoField(2)] public string Label { get; set; } = "";
}

[ProtoContract]
public class NullableMessage
{
    [ProtoField(1)] public int? NullableInt { get; set; }
    [ProtoField(2)] public bool? NullableBool { get; set; }
    [ProtoField(3)] public double? NullableDouble { get; set; }
}

[ProtoContract]
public class NestedOuter
{
    [ProtoField(1)] public string Title { get; set; } = "";
    [ProtoField(2)] public NestedInner Inner { get; set; } = new();
}

[ProtoContract]
public class NestedInner
{
    [ProtoField(1)] public int Value { get; set; }
    [ProtoField(2)] public string Detail { get; set; } = "";
}

[ProtoContract]
public class DeepNested
{
    [ProtoField(1)] public string Level { get; set; } = "";
    [ProtoField(2)] public NestedOuter Outer { get; set; } = new();
}

[ProtoContract]
public class ListMessage
{
    [ProtoField(1)] public List<int> Numbers { get; set; } = [];
    [ProtoField(2)] public List<string> Tags { get; set; } = [];
    [ProtoField(3)] public List<NestedInner> Items { get; set; } = [];
}

[ProtoContract]
public class ArrayMessage
{
    [ProtoField(1)] public int[] Scores { get; set; } = [];
    [ProtoField(2)] public string[] Names { get; set; } = [];
}

[ProtoContract]
public class MapMessage
{
    [ProtoMap]
    [ProtoField(1)] public Dictionary<string, string> Tags { get; set; } = new();

    [ProtoMap]
    [ProtoField(2)] public Dictionary<int, NestedInner> ItemMap { get; set; } = new();
}

[ProtoContract]
public class OneOfMessage
{
    [ProtoOneOf("contact")]
    [ProtoField(1)] public string? Email { get; set; }

    [ProtoOneOf("contact")]
    [ProtoField(2)] public string? Phone { get; set; }

    [ProtoField(3)] public string Name { get; set; } = "";
}

[ProtoContract]
[ProtoInclude(10, typeof(DogModel))]
[ProtoInclude(11, typeof(CatModel))]
public class AnimalModel
{
    [ProtoField(1)] public string Name { get; set; } = "";
}

[ProtoContract(IncludeBaseFields = true)]
public class DogModel : AnimalModel
{
    [ProtoField(2)] public string Breed { get; set; } = "";
}

[ProtoContract(IncludeBaseFields = true)]
public class CatModel : AnimalModel
{
    [ProtoField(2)] public bool IsIndoor { get; set; }
}

[ProtoContract(ExplicitFields = true)]
public class ExplicitMessage
{
    [ProtoField(1)] public int Included { get; set; }
    public string Excluded { get; set; } = ""; // no [ProtoField] => excluded
    [ProtoField(3)] public string AlsoIncluded { get; set; } = "";
}

[ProtoContract]
public class IgnoredFieldMessage
{
    [ProtoField(1)] public string Visible { get; set; } = "";
    [ProtoIgnore] public string Hidden { get; set; } = "";
}

[ProtoContract]
public class RequiredFieldMessage
{
    [ProtoField(FieldNumber = 1, IsRequired = true)]
    public string MustHaveValue { get; set; } = "";

    [ProtoField(2)] public int Optional { get; set; }
}

[ProtoContract]
public class WriteDefaultMessage
{
    [ProtoField(FieldNumber = 1, WriteDefault = true)]
    public int AlwaysWritten { get; set; }

    [ProtoField(2)] public int SkippedWhenDefault { get; set; }
}

[ProtoContract(ImplicitFields = true)]
public class ImplicitParent
{
    [ProtoField(1)] public string Title { get; set; } = "";
    [ProtoField(2)] public ImplicitChild Child { get; set; } = new();
}

public class ImplicitChild // no [ProtoContract] - implicit
{
    public int Value { get; set; }
    public string Label { get; set; } = "";
}

[ProtoContract("NamedContract", Version = 3)]
public class VersionedModel
{
    [ProtoField(1)] public string Data { get; set; } = "";
}

[ProtoContract(1)]
public class VersionOnlyModel
{
    [ProtoField(1)] public int Id { get; set; }
}

[ProtoContract]
[ProtoInclude(10, typeof(AdminProfile))]
public class UserProfileModel
{
    [ProtoField(1)] public string DisplayName { get; set; } = "";
    [ProtoField(2)] public int Age { get; set; }
}

[ProtoContract(IncludeBaseFields = true)]
public class AdminProfile : UserProfileModel
{
    [ProtoField(3)] public string Department { get; set; } = "";
}

[ProtoContract]
public class LargeFieldMessage
{
    [ProtoField(536_870_911)] // Max possible proto field number
    public int VeryLargeField { get; set; }
}
