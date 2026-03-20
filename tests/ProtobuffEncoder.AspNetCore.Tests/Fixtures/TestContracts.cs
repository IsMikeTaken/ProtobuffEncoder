using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.AspNetCore.Tests.Fixtures;

[ProtoContract]
public class TestRequest
{
    [ProtoField(1)] public int Id { get; set; }
    [ProtoField(2)] public string Name { get; set; } = "";
}

[ProtoContract]
public class TestResponse
{
    [ProtoField(1)] public int Id { get; set; }
    [ProtoField(2)] public string Result { get; set; } = "";
    [ProtoField(3)] public bool Success { get; set; }
}

[ProtoContract]
public class EmptyContract
{
}

/// <summary>No parameterless constructor — cannot be used with InputFormatter.</summary>
public class NoDefaultCtorModel
{
    public NoDefaultCtorModel(int id) => Id = id;
    public int Id { get; }
}
