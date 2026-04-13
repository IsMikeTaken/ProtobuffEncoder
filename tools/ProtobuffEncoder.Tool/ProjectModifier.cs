using System.Xml.Linq;

namespace ProtobuffEncoder.Tool;

internal static class ProjectModifier
{
    /// <summary>
    /// Adds <c>&lt;Content Include="..." CopyToOutputDirectory="PreserveNewest" /&gt;</c> entries
    /// for each generated .proto file.  Paths already present in the project are skipped.
    /// </summary>
    public static void AppendToCsproj(
        string csprojPath,
        IReadOnlyList<(string RelativePath, string AbsolutePath)> generated)
    {
        var doc  = XDocument.Load(csprojPath);
        var root = doc.Root;
        if (root is null) return;

        var ns         = root.GetDefaultNamespace();
        var csprojDir  = Path.GetDirectoryName(Path.GetFullPath(csprojPath))!;

        var existingIncludes = root
            .Descendants(ns + "Content")
            .Concat(root.Descendants(ns + "None"))
            .Select(e => NormaliseRelative(e.Attribute("Include")?.Value))
            .Where(v => v is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = generated
            .Select(g =>
            {
                var rel = NormaliseRelative(Path.GetRelativePath(csprojDir, g.AbsolutePath))!;
                return (rel, already: existingIncludes.Contains(rel));
            })
            .Where(x => !x.already)
            .Select(x => x.rel)
            .ToList();

        if (toAdd.Count == 0) return;

        // Group by directory so we can emit tidy ItemGroup blocks
        var byDir = toAdd.GroupBy(p => Path.GetDirectoryName(p) ?? "");

        foreach (var group in byDir)
        {
            var itemGroup = new XElement(ns + "ItemGroup");

            foreach (var rel in group.OrderBy(p => p))
            {
                itemGroup.Add(new XElement(ns + "Content",
                    new XAttribute("Include", rel.Replace('/', '\\')),
                    new XElement(ns + "CopyToOutputDirectory", "PreserveNewest")));
            }

            root.Add(itemGroup);
        }

        doc.Save(csprojPath);
    }

    private static string? NormaliseRelative(string? path) =>
        path?.Replace('\\', '/');
}
