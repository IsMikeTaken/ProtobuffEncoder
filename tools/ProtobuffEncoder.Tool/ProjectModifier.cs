using System.Xml.Linq;

namespace ProtobuffEncoder.Tool;

internal static class ProjectModifier
{
    public static void AppendToCsproj(string csprojPath, string outputDir, List<string> generatedPaths)
    {
        var doc = XDocument.Load(csprojPath);
        var root = doc.Root;
        if (root is null) return;

        var ns = root.GetDefaultNamespace();
        var projectDir = Path.GetDirectoryName(csprojPath)!;

        // Find or create the ItemGroup for proto files
        var protoItemGroup = root.Elements(ns + "ItemGroup")
            .FirstOrDefault(ig => ig.Elements(ns + "Content")
                .Any(e => e.Attribute("Include")?.Value.EndsWith(".proto", StringComparison.OrdinalIgnoreCase) == true))
            ?? root.Elements(ns + "ItemGroup")
            .FirstOrDefault(ig => ig.Elements(ns + "None")
                .Any(e => e.Attribute("Include")?.Value.EndsWith(".proto", StringComparison.OrdinalIgnoreCase) == true));

        if (protoItemGroup is null)
        {
            protoItemGroup = new XElement(ns + "ItemGroup",
                new XComment(" Auto-generated proto schemas "));
            root.Add(protoItemGroup);
        }

        var existingIncludes = protoItemGroup.Elements()
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => v is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool modified = false;
        foreach (var fullPath in generatedPaths)
        {
            var relativePath = Path.GetRelativePath(projectDir, fullPath).Replace('/', '\\');

            if (existingIncludes.Contains(relativePath))
                continue;

            protoItemGroup.Add(new XElement(ns + "Content",
                new XAttribute("Include", relativePath),
                new XElement(ns + "CopyToOutputDirectory", "PreserveNewest")));

            modified = true;
        }

        if (modified)
        {
            doc.Save(csprojPath);
        }
    }
}
