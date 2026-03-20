namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Marks an interface or class as a gRPC service definition.
/// Methods decorated with <see cref="ProtoMethodAttribute"/> define the RPC operations.
/// The service name is used as the gRPC service identifier in the method's full path
/// (<c>/ServiceName/MethodName</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = true)]
public sealed class ProtoServiceAttribute : Attribute
{
    public ProtoServiceAttribute(string serviceName)
    {
        ServiceName = serviceName;
    }

    /// <summary>The gRPC service name (used in the <c>/ServiceName/MethodName</c> route).</summary>
    public string ServiceName { get; }

    /// <summary>
    /// The API version for service schema generation. If > 0, placed in v{Version}/ directory.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Descriptive metadata comment to include in the generated .proto service definition.
    /// </summary>
    public string? Metadata { get; set; }
}
