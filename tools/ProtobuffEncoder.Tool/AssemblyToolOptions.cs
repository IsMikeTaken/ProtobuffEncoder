using System.Reflection;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Tool;

internal sealed class AssemblyToolOptions
{
    private const string DefaultProtoPath = "Contracts/Proto";

    public string ProtoPath { get; }
    private readonly ProtoRouteAttribute[] _routes;

    private AssemblyToolOptions(string protoPath, ProtoRouteAttribute[] routes)
    {
        ProtoPath = protoPath;
        _routes   = routes;
    }

    public static AssemblyToolOptions Read(Assembly assembly)
    {
        var protoPath = assembly.GetCustomAttribute<ProtoToolOptionsAttribute>() is { } opts
            ? NormalisePath(opts.ProtoPath)
            : DefaultProtoPath;

        var routes = assembly.GetCustomAttributes<ProtoRouteAttribute>().ToArray();

        return new AssemblyToolOptions(protoPath, routes);
    }

    /// <summary>
    /// Builds the output path for a generated file.
    /// Layout: {ProtoPath}/{routeFolder?}/{versionDir?}/{filename}
    /// <paramref name="protoFileName"/> may already carry a version prefix, e.g. <c>"v2/weather.proto"</c>.
    /// </summary>
    public string ResolveOutputPath(string protoFileName, string primaryTypeName)
    {
        var dir  = Path.GetDirectoryName(protoFileName) ?? "";
        var file = Path.GetFileName(protoFileName);

        // Build path segments in order: root / route / version-dir / file
        var capacity = 2 + (string.IsNullOrEmpty(dir) ? 0 : 1) + 1;
        var segments = new List<string>(capacity) { ProtoPath };

        var route = MatchRoute(primaryTypeName);
        if (route is not null) segments.Add(route);
        if (!string.IsNullOrEmpty(dir)) segments.Add(dir);
        segments.Add(file);

        return Path.Combine([.. segments]);
    }

    private string? MatchRoute(string typeName)
    {
        foreach (var route in _routes)
        {
            if (route.Matches(typeName)) return route.Folder;
        }
        return null;
    }

    private static string NormalisePath(string path) =>
        path.Replace('\\', '/').Trim('/');
}
