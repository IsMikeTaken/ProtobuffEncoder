namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Routes generated .proto files whose primary type name matches a token into a subfolder.
/// Apply one attribute per routing rule at the assembly level alongside
/// <see cref="ProtoToolOptionsAttribute"/>.
/// </summary>
/// <remarks>
/// A type matches when its unqualified name <i>starts with</i> or <i>ends with</i>
/// any of the supplied tokens (case-insensitive).  Rules are evaluated in declaration order;
/// first match wins.  Types that match no rule land directly in
/// <see cref="ProtoToolOptionsAttribute.ProtoPath"/>.
/// </remarks>
/// <example>
/// <code>
/// [assembly: ProtoToolOptions(ProtoPath = "Contracts/Proto")]
/// [assembly: ProtoRoute("requests",  "Request", "Query")]
/// [assembly: ProtoRoute("responses", "Response", "Result")]
/// [assembly: ProtoRoute("messages",  "Message",  "Event", "Notification")]
/// [assembly: ProtoRoute("services",  "Service")]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ProtoRouteAttribute : Attribute
{
    /// <summary>
    /// Subfolder relative to <see cref="ProtoToolOptionsAttribute.ProtoPath"/>,
    /// e.g. <c>"requests"</c>.
    /// </summary>
    public string Folder { get; }

    /// <summary>
    /// Name tokens to match against type names (prefix or suffix, case-insensitive).
    /// </summary>
    public string[] Tokens { get; }

    /// <param name="folder">Target subfolder, e.g. <c>"requests"</c>.</param>
    /// <param name="tokens">One or more tokens, e.g. <c>"Request"</c>, <c>"Query"</c>.</param>
    public ProtoRouteAttribute(string folder, params string[] tokens)
    {
        Folder = folder;
        Tokens = tokens;
    }

    internal bool Matches(string typeName) =>
        Tokens.Any(t =>
            typeName.StartsWith(t, StringComparison.OrdinalIgnoreCase) ||
            typeName.EndsWith(t, StringComparison.OrdinalIgnoreCase));
}
