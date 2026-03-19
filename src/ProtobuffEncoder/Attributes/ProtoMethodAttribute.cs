namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Marks a method on a <see cref="ProtoServiceAttribute"/>-decorated interface as an RPC operation.
/// The method's parameter and return types define the request/response message types.
/// <para>
/// <b>Method signature patterns:</b>
/// <list type="bullet">
///   <item><b>Unary:</b> <c>Task&lt;TResponse&gt; Method(TRequest request)</c></item>
///   <item><b>ServerStreaming:</b> <c>IAsyncEnumerable&lt;TResponse&gt; Method(TRequest request, CancellationToken ct)</c></item>
///   <item><b>ClientStreaming:</b> <c>Task&lt;TResponse&gt; Method(IAsyncEnumerable&lt;TRequest&gt; stream, CancellationToken ct)</c></item>
///   <item><b>DuplexStreaming:</b> <c>IAsyncEnumerable&lt;TResponse&gt; Method(IAsyncEnumerable&lt;TRequest&gt; stream, CancellationToken ct)</c></item>
/// </list>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ProtoMethodAttribute : Attribute
{
    public ProtoMethodAttribute(ProtoMethodType methodType)
    {
        MethodType = methodType;
    }

    /// <summary>The type of gRPC call (Unary, ServerStreaming, ClientStreaming, DuplexStreaming).</summary>
    public ProtoMethodType MethodType { get; }

    /// <summary>
    /// Override the method name in the gRPC route. Defaults to the C# method name.
    /// </summary>
    public string? Name { get; set; }
}
