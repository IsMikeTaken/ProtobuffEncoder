using System.Reflection;
using ProtobuffEncoder.Attributes;

namespace ProtobuffEncoder.Tool;

/// <summary>
/// Reads <see cref="ProtoToolOptionsAttribute"/> and <see cref="ProtoRouteAttribute"/>
/// from a loaded assembly and exposes the resolved output-path logic used by
/// <c>proto-gen</c> when writing generated <c>.proto</c> files.
/// </summary>
internal sealed class AssemblyToolOptions
{
    private const string DefaultProtoPath = "Contracts/Proto";

    /// <summary>Gets the root output folder for generated <c>.proto</c> files.</summary>
    /// <remarks>Defaults to <c>Contracts/Proto</c> when no <see cref="ProtoToolOptionsAttribute"/> is present.</remarks>
    public string ProtoPath { get; }

    private readonly ProtoRouteAttribute[] _routes;

    private AssemblyToolOptions(string protoPath, ProtoRouteAttribute[] routes)
    {
        ProtoPath = protoPath;
        _routes = routes;
    }

    /// <summary>
    /// Reads tool configuration from the given <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">The loaded assembly to inspect.</param>
    /// <returns>
    /// An <see cref="AssemblyToolOptions"/> instance populated from any
    /// <see cref="ProtoToolOptionsAttribute"/> and <see cref="ProtoRouteAttribute"/>
    /// declarations on the assembly, or sensible defaults when absent.
    /// </returns>
    /// <example>
    /// Given the following assembly attributes:
    /// <code>
    /// [assembly: ProtoToolOptions(ProtoPath = "Contracts/Proto")]
    /// [assembly: ProtoRoute("requests",  "Request", "Query")]
    /// [assembly: ProtoRoute("responses", "Response", "Result")]
    /// </code>
    /// <c>Read(assembly).ProtoPath</c> returns <c>"Contracts/Proto"</c> and
    /// <c>ResolveOutputPath("weather.proto", "WeatherRequest")</c> returns
    /// <c>"Contracts/Proto/requests/weather.proto"</c>.
    /// </example>
    public static AssemblyToolOptions Read(Assembly assembly)
    {
        var protoPath = assembly.GetCustomAttribute<ProtoToolOptionsAttribute>() is { } opts
            ? NormalisePath(opts.ProtoPath)
            : DefaultProtoPath;

        var routes = assembly.GetCustomAttributes<ProtoRouteAttribute>().ToArray();

        return new AssemblyToolOptions(protoPath, routes);
    }

    /// <summary>
    /// Builds the output path for a generated <c>.proto</c> file.
    /// </summary>
    /// <param name="protoFileName">
    /// The file key returned by the schema generator, e.g. <c>"weather.proto"</c>
    /// or <c>"v2/weather.proto"</c> for versioned schemas.
    /// </param>
    /// <param name="primaryTypeName">
    /// The C# type name of the primary contract in the file, used to match
    /// <see cref="ProtoRouteAttribute"/> rules (e.g. <c>"WeatherRequest"</c>).
    /// </param>
    /// <returns>
    /// A relative path suitable for use as a <c>&lt;Content Include="..."&gt;</c>
    /// value, combining <see cref="ProtoPath"/>, an optional route subfolder,
    /// an optional version directory, and the file name.
    /// </returns>
    /// <example>
    /// With <c>ProtoPath = "Contracts/Proto"</c> and a route
    /// <c>[assembly: ProtoRoute("requests", "Request")]</c>:
    /// <code>
    /// ResolveOutputPath("weather.proto", "WeatherRequest")
    ///     // → "Contracts/Proto/requests/weather.proto"
    ///
    /// ResolveOutputPath("v2/weather.proto", "WeatherRequest")
    ///     // → "Contracts/Proto/requests/v2/weather.proto"
    ///
    /// ResolveOutputPath("weather.proto", "WeatherForecast")
    ///     // → "Contracts/Proto/weather.proto"  (no matching route)
    /// </code>
    /// </example>
    public string ResolveOutputPath(string protoFileName, string primaryTypeName)
    {
        var dir = Path.GetDirectoryName(protoFileName) ?? "";
        var file = Path.GetFileName(protoFileName);

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
