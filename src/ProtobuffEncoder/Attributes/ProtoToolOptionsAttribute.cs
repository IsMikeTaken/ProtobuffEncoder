namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Assembly-level attribute that configures how the <c>ProtobuffEncoder.Tool</c> generates
/// and places .proto files for this assembly.
/// </summary>
/// <example>
/// Place in any .cs file (commonly <c>AssemblyInfo.cs</c> or <c>ProtoConfig.cs</c>):
/// <code>
/// [assembly: ProtoToolOptions(
///     ProtoPath = "Contracts/Proto",
///     Routes = [
///         new ProtoTypeRoute("requests",  "Request", "Query"),
///         new ProtoTypeRoute("responses", "Response", "Result"),
///         new ProtoTypeRoute("messages",  "Message", "Event"),
///     ]
/// )]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ProtoToolOptionsAttribute : Attribute
{
    /// <summary>
    /// Root output folder for generated .proto files, relative to the project directory.
    /// Defaults to <c>"Protos"</c> when not set.
    /// </summary>
    public string ProtoPath { get; set; } = "Protos";

    /// <summary>
    /// Routing rules that map type-name patterns to subfolders inside <see cref="ProtoPath"/>.
    /// Types that match no rule land directly in <see cref="ProtoPath"/>.
    /// Rules are evaluated in declaration order; first match wins.
    /// </summary>
    public ProtoTypeRoute[] Routes { get; set; } = [];
}
