using System.Reflection;
using System.Xml.Linq;
using ProtobuffEncoder.Schema;
using ProtobuffEncoder.Tool;

// Usage: dotnet run -- <assembly-path> <proto-output-dir> [csproj-path] [--verbose]
if (args.Length < 2 || args.Contains("--help") || args.Contains("-h"))
{
    Console.Error.WriteLine("Usage: ProtobuffEncoder.Tool <assembly-path> <proto-output-dir> [csproj-path] [--verbose]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  assembly-path     Path to the compiled DLL containing [ProtoContract] types");
    Console.Error.WriteLine("  proto-output-dir  Directory to write generated .proto files");
    Console.Error.WriteLine("  csproj-path       (optional) .csproj file to auto-append proto file references");
    Console.Error.WriteLine("  --verbose         Show import relationships and service details");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Features:");
    Console.Error.WriteLine("  - Auto-generates .proto files from [ProtoContract] types");
    Console.Error.WriteLine("  - Auto-generates service definitions from [ProtoService] interfaces");
    Console.Error.WriteLine("  - Auto-resolves cross-file imports when types reference other files");
    Console.Error.WriteLine("  - Version-based directory structure (v1/, v2/, etc.)");
    if (args.Length < 2)
        return 1;
    return 0;
}

var positionalArgs = args.Where(a => !a.StartsWith("--")).ToArray();
bool verbose = args.Contains("--verbose");

var assemblyPath = Path.GetFullPath(positionalArgs[0]);
var outputDir = Path.GetFullPath(positionalArgs[1]);
var csprojPath = positionalArgs.Length >= 3 ? Path.GetFullPath(positionalArgs[2]) : null;

if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
    return 1;
}

// Load the assembly
var assembly = Assembly.LoadFrom(assemblyPath);

// Generate all .proto files (with auto-import resolution)
var protoFiles = ProtoSchemaGenerator.GenerateAll(assembly);

if (protoFiles.Count == 0)
{
    Console.WriteLine("No [ProtoContract] or [ProtoService] types found.");
    return 0;
}

// Write to a directory
var generatedPaths = ProtoSchemaGenerator.GenerateToDirectory(assembly, outputDir);

foreach (var path in generatedPaths)
{
    Console.WriteLine($"  Generated: {Path.GetRelativePath(outputDir, path)}");
}

// Show import/service details in verbose mode
if (verbose)
{
    Console.WriteLine();
    foreach (var (filename, content) in protoFiles.OrderBy(kv => kv.Key))
    {
        var importCount = content.Split('\n').Count(l => l.TrimStart().StartsWith("import "));
        var serviceCount = content.Split('\n').Count(l => l.TrimStart().StartsWith("service "));
        var messageCount = content.Split('\n').Count(l => l.TrimStart().StartsWith("message "));

        if (importCount > 0 || serviceCount > 0)
        {
            Console.WriteLine($"  {filename}: {messageCount} message(s), {serviceCount} service(s), {importCount} import(s)");
        }
    }
}

// Auto-append to .csproj if provided
if (csprojPath is not null && File.Exists(csprojPath))
{
    ProjectModifier.AppendToCsproj(csprojPath, outputDir, generatedPaths);
    Console.WriteLine($"  Updated csproj: {csprojPath}");
}

Console.WriteLine($"Done. Generated {generatedPaths.Count} .proto file(s) in {outputDir}");
return 0;
