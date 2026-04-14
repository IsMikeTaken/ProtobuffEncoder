using System.Xml.Linq;

namespace ProtobuffEncoder.Tool;

/// <summary>
/// Patches <c>.csproj</c> files to include generated <c>.proto</c> files
/// as <c>&lt;Content&gt;</c> items with <c>CopyToOutputDirectory="PreserveNewest"</c>.
/// </summary>
internal static class ProjectModifier
{
    /// <summary>
    /// Appends <c>&lt;Content Include="..." /&gt;</c> entries for each generated
    /// <c>.proto</c> file to the given <c>.csproj</c>.
    /// </summary>
    /// <param name="csprojPath">Absolute path to the <c>.csproj</c> file to update.</param>
    /// <param name="generated">
    /// Collection of <c>(RelativePath, AbsolutePath)</c> pairs for the written files.
    /// <c>RelativePath</c> is stored as the <c>Include</c> attribute value;
    /// <c>AbsolutePath</c> is used only to confirm the file was actually written.
    /// </param>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>Entries already present in the project (case-insensitive, slash-normalised) are skipped.</item>
    ///   <item>New entries are grouped into one <c>&lt;ItemGroup&gt;</c> per output directory and sorted alphabetically.</item>
    ///   <item>The method is a no-op when <paramref name="generated"/> is empty or all entries already exist.</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// After calling this method the project file will contain:
    /// <code language="xml">
    /// &lt;ItemGroup&gt;
    ///   &lt;Content Include="Contracts\Proto\requests\weather.proto"&gt;
    ///     &lt;CopyToOutputDirectory&gt;PreserveNewest&lt;/CopyToOutputDirectory&gt;
    ///   &lt;/Content&gt;
    /// &lt;/ItemGroup&gt;
    /// </code>
    /// </example>
    public static void AppendToCsproj(
        string csprojPath,
        IReadOnlyList<(string RelativePath, string AbsolutePath)> generated)
    {
        if (generated.Count == 0) return;

        var doc  = XDocument.Load(csprojPath);
        var root = doc.Root;
        if (root is null) return;

        var ns = root.GetDefaultNamespace();

        var existingIncludes = root
            .Descendants(ns + "Content")
            .Concat(root.Descendants(ns + "None"))
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => v is not null)
            .Select(v => Normalise(v!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = generated
            .Where(g => !existingIncludes.Contains(Normalise(g.RelativePath)))
            .Select(g => g.RelativePath)
            .ToList();

        if (toAdd.Count == 0) return;

        foreach (var group in toAdd.GroupBy(p => Path.GetDirectoryName(p) ?? ""))
        {
            var itemGroup = new XElement(ns + "ItemGroup");

            foreach (var rel in group.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                itemGroup.Add(new XElement(ns + "Content",
                    new XAttribute("Include", rel.Replace('/', '\\')),
                    new XElement(ns + "CopyToOutputDirectory", "PreserveNewest")));
            }

            root.Add(itemGroup);
        }

        doc.Save(csprojPath);
    }

    private static string Normalise(string path) => path.Replace('\\', '/');
}
