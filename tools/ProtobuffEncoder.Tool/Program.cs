using System.Reflection;
using System.Xml.Linq;
using ProtobuffEncoder.Schema;

// Usage: dotnet run -- <assembly-path> <proto-output-dir> [csproj-path]
if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: ProtobuffEncoder.Tool <assembly-path> <proto-output-dir> [csproj-path]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  assembly-path   Path to the compiled DLL containing [ProtoContract] types");
    Console.Error.WriteLine("  proto-output-dir  Directory to write generated .proto files");
    Console.Error.WriteLine("  csproj-path     (optional) .csproj file to auto-append proto file references");
    return 1;
}

var assemblyPath = Path.GetFullPath(args[0]);
var outputDir = Path.GetFullPath(args[1]);
var csprojPath = args.Length >= 3 ? Path.GetFullPath(args[2]) : null;

if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
    return 1;
}

// Load the assembly
var assembly = Assembly.LoadFrom(assemblyPath);

// Generate .proto files
var generatedPaths = ProtoSchemaGenerator.GenerateToDirectory(assembly, outputDir);

if (generatedPaths.Count == 0)
{
    Console.WriteLine("No [ProtoContract] types found.");
    return 0;
}

foreach (var path in generatedPaths)
{
    Console.WriteLine($"  Generated: {path}");
}

// Auto-append to .csproj if provided
if (csprojPath is not null && File.Exists(csprojPath))
{
    AppendToCsproj(csprojPath, outputDir, generatedPaths);
}

Console.WriteLine($"Done. Generated {generatedPaths.Count} .proto file(s) in {outputDir}");
return 0;

static void AppendToCsproj(string csprojPath, string outputDir, List<string> generatedPaths)
{
    var doc = XDocument.Load(csprojPath);
    var root = doc.Root;
    if (root is null) return;

    var ns = root.GetDefaultNamespace();
    var projectDir = Path.GetDirectoryName(csprojPath)!;

    // Find or create the ItemGroup for proto files (look for one that already has Content with .proto)
    var protoItemGroup = root.Elements(ns + "ItemGroup")
        .FirstOrDefault(ig => ig.Elements(ns + "Content")
            .Any(e => e.Attribute("Include")?.Value.EndsWith(".proto") == true));

    if (protoItemGroup is null)
    {
        // Also check for None items with .proto
        protoItemGroup = root.Elements(ns + "ItemGroup")
            .FirstOrDefault(ig => ig.Elements(ns + "None")
                .Any(e => e.Attribute("Include")?.Value.EndsWith(".proto") == true));
    }

    if (protoItemGroup is null)
    {
        protoItemGroup = new XElement(ns + "ItemGroup",
            new XComment(" Auto-generated proto schemas "));
        root.Add(protoItemGroup);
    }

    // Get all existing proto includes
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

        Console.WriteLine($"  Added to csproj: {relativePath}");
        modified = true;
    }

    if (modified)
    {
        doc.Save(csprojPath);
    }
}
