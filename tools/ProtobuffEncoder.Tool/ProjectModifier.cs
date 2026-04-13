using System.Xml.Linq;

namespace ProtobuffEncoder.Tool;

internal static class ProjectModifier
{
    /// <summary>
    /// Adds <c>&lt;Content Include="..." CopyToOutputDirectory="PreserveNewest" /&gt;</c> entries
    /// for each generated .proto file. Entries already present (case-insensitive) are skipped.
    /// Files are grouped by directory into separate <c>&lt;ItemGroup&gt;</c> blocks.
    /// </summary>
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

        // Filter to new-only entries using the caller-supplied relative path.
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

    // Normalise slashes so forward/back variants compare equal.
    private static string Normalise(string path) => path.Replace('\\', '/');
}
