namespace ProtobuffEncoder.Attributes;

/// <summary>
/// Describes how a naming pattern maps generated .proto files into a subfolder.
/// Apply multiple <see cref="ProtoTypeRoute"/> entries to <see cref="ProtoToolOptionsAttribute"/>
/// to route different message categories into separate directories.
/// </summary>
public sealed class ProtoTypeRoute
{
    /// <summary>
    /// The subfolder (relative to <see cref="ProtoToolOptionsAttribute.ProtoPath"/>) into
    /// which matching types will be written.  Supports forward or back slashes.
    /// </summary>
    public string Folder { get; }

    /// <summary>
    /// One or more name tokens a type must contain to be routed here.
    /// A type matches when its unqualified name <i>starts with</i> or <i>ends with</i>
    /// any of the supplied tokens (case-insensitive).
    /// </summary>
    public string[] Tokens { get; }

    /// <param name="folder">Target subfolder, e.g. <c>"requests"</c>.</param>
    /// <param name="tokens">
    /// Name tokens to match against, e.g. <c>"Request", "Query"</c>.
    /// </param>
    public ProtoTypeRoute(string folder, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("Folder must not be empty.", nameof(folder));
        if (tokens is null || tokens.Length == 0)
            throw new ArgumentException("At least one token is required.", nameof(tokens));

        Folder = folder.Replace('\\', '/').TrimEnd('/');
        Tokens = tokens;
    }

    /// <summary>
    /// Returns true when <paramref name="typeName"/> starts with or ends with
    /// any of <see cref="Tokens"/> (case-insensitive).
    /// </summary>
    public bool Matches(string typeName) =>
        Tokens.Any(t =>
            typeName.StartsWith(t, StringComparison.OrdinalIgnoreCase) ||
            typeName.EndsWith(t, StringComparison.OrdinalIgnoreCase));
}
