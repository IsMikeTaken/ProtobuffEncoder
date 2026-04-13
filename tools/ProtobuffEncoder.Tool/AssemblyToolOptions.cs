using System.Reflection;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Tool;

/// <summary>
/// Reads <see cref="ProtoToolOptionsAttribute"/> from a loaded assembly and exposes
/// the resolved <see cref="ProtoPath"/> and routing logic used during generation.
/// </summary>
internal sealed class AssemblyToolOptions
{
    public string ProtoPath { get; }
    private readonly ProtoTypeRoute[] _routes;

    private AssemblyToolOptions(string protoPath, ProtoTypeRoute[] routes)
    {
        ProtoPath = protoPath;
        _routes = routes;
    }

    public static AssemblyToolOptions Read(Assembly assembly)
    {
        var attr = assembly.GetCustomAttribute<ProtoToolOptionsAttribute>();
        if (attr is null)
            return new AssemblyToolOptions("Protos", []);

        return new AssemblyToolOptions(
            NormalisePath(attr.ProtoPath),
            attr.Routes ?? []);
    }

    /// <summary>
    /// Returns the output path for a generated file whose base name is
    /// <paramref name="protoFileName"/> (e.g. <c>"weather.proto"</c>).
    /// The path is relative to the project directory and incorporates any
    /// version subfolder already embedded in <paramref name="protoFileName"/>.
    /// </summary>
    public string ResolveOutputPath(string protoFileName, string primaryTypeName)
    {
        // protoFileName may already contain a version dir, e.g. "v2/weather.proto"
        var dir = Path.GetDirectoryName(protoFileName) ?? "";
        var file = Path.GetFileName(protoFileName);

        var routeFolder = MatchRoute(primaryTypeName);

        var segments = new List<string> { ProtoPath };
        if (!string.IsNullOrEmpty(dir))
            segments.Add(dir);
        if (!string.IsNullOrEmpty(routeFolder))
            segments.Add(routeFolder);

        return Path.Combine([.. segments, file]);
    }

    private string? MatchRoute(string typeName)
    {
        foreach (var route in _routes)
        {
            if (route.Matches(typeName))
                return route.Folder;
        }
        return null;
    }

    private static string NormalisePath(string path) =>
        path.Replace('\\', '/').Trim('/');
}
