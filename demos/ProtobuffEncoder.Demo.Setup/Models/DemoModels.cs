using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Demo.Setup.Models;

[ProtoContract]
public class DemoRequest
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public int Value { get; set; }
}

[ProtoContract]
public class DemoResponse
{
    [ProtoMember(1)]
    public string Message { get; set; } = string.Empty;

    [ProtoMember(2)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

[ProtoService("DemoService")]
public interface IDemoService
{
    [ProtoMethod]
    Task<DemoResponse> SayHello(DemoRequest request);
}
