namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Assembly-level attribute that sets the root output path for generated .proto files.
/// Pair with one or more <see cref="ProtoRouteAttribute"/> declarations to route types
/// into subfolders.
/// </summary>
/// <example>
/// <code>
/// [assembly: ProtoToolOptions(ProtoPath = "Contracts/Proto")]
/// [assembly: ProtoRoute("requests",  "Request", "Query")]
/// [assembly: ProtoRoute("responses", "Response", "Result")]
/// [assembly: ProtoRoute("messages",  "Message",  "Event", "Notification")]
/// [assembly: ProtoRoute("services",  "Service")]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ProtoToolOptionsAttribute : Attribute
{
    /// <summary>
    /// Root output folder for generated .proto files, relative to the project directory.
    /// Defaults to <c>"Contracts/Proto"</c> when not set.
    /// </summary>
    public string ProtoPath { get; set; } = "Contracts/Proto";
}
