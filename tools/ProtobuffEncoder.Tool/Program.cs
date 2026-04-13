using System.Reflection;
using ProtobuffEncoder.Schema;
using ProtobuffEncoder.Tool;

if (args.Length < 1 || args.Contains("--help") || args.Contains("-h"))
{
    PrintUsage();
    return args.Length < 1 ? 1 : 0;
}

var positional = args.Where(a => !a.StartsWith("--")).ToArray();
bool verbose = args.Contains("--verbose");
bool dryRun  = args.Contains("--dry-run");

var assemblyPath = Path.GetFullPath(positional[0]);
// output-dir and csproj are optional — the assembly attribute drives defaults
var explicitOutputDir = positional.Length >= 2 ? Path.GetFullPath(positional[1]) : null;
var csprojPath        = positional.Length >= 3 ? Path.GetFullPath(positional[2]) : null;

if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"error: assembly not found: {assemblyPath}");
    return 1;
}

var assembly = Assembly.LoadFrom(assemblyPath);
var toolOptions = AssemblyToolOptions.Read(assembly);

// If no explicit output dir, derive it from the assembly location + ProtoPath
var projectDir = explicitOutputDir is not null
    ? Path.GetDirectoryName(Path.GetFullPath(explicitOutputDir)) ?? Directory.GetCurrentDirectory()
    : Path.GetDirectoryName(assemblyPath) ?? Directory.GetCurrentDirectory();

var baseOutputDir = explicitOutputDir
    ?? Path.Combine(projectDir, toolOptions.ProtoPath);

var protoFiles = ProtoSchemaGenerator.GenerateAll(assembly);

if (protoFiles.Count == 0)
{
    Console.WriteLine("No [ProtoContract] or [ProtoService] types found.");
    return 0;
}

// Determine primary type names per file key so we can apply routing
var primaryTypeNames = ResolvePrimaryTypeNames(assembly, protoFiles.Keys);

var written = new List<(string RelativePath, string AbsolutePath)>();

foreach (var (fileKey, content) in protoFiles.OrderBy(kv => kv.Key))
{
    var primaryType = primaryTypeNames.TryGetValue(fileKey, out var name) ? name : fileKey;
    var relPath = toolOptions.ResolveOutputPath(fileKey, primaryType);
    var absPath = Path.GetFullPath(Path.Combine(projectDir, relPath));

    if (dryRun)
    {
        Console.WriteLine($"  [dry-run] would write: {relPath}");
        continue;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
    File.WriteAllText(absPath, content);
    written.Add((relPath, absPath));

    Console.WriteLine($"  Generated: {relPath}");

    if (verbose)
    {
        var lines    = content.Split('\n');
        var imports  = lines.Count(l => l.TrimStart().StartsWith("import "));
        var services = lines.Count(l => l.TrimStart().StartsWith("service "));
        var messages = lines.Count(l => l.TrimStart().StartsWith("message "));
        Console.WriteLine($"             {messages} message(s), {services} service(s), {imports} import(s)");
    }
}

if (!dryRun && written.Count > 0 && csprojPath is not null && File.Exists(csprojPath))
{
    ProjectModifier.AppendToCsproj(csprojPath, written);
    Console.WriteLine($"  Updated:   {Path.GetRelativePath(Directory.GetCurrentDirectory(), csprojPath)}");
}

Console.WriteLine($"Done. {(dryRun ? "Would generate" : "Generated")} {protoFiles.Count} .proto file(s).");
return 0;

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: ProtobuffEncoder.Tool <assembly-path> [output-dir] [csproj-path] [options]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  assembly-path   Compiled DLL with [ProtoContract] / [ProtoService] types");
    Console.Error.WriteLine("  output-dir      Override base output directory (default: from [ProtoToolOptions])");
    Console.Error.WriteLine("  csproj-path     .csproj to update with <Content Include=...> entries");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options:");
    Console.Error.WriteLine("  --verbose       Show per-file message / service / import counts");
    Console.Error.WriteLine("  --dry-run       Print what would be written without touching the file system");
    Console.Error.WriteLine("  --help, -h      Show this help");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Assembly-level configuration (place in any .cs file in your project):");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  [assembly: ProtoToolOptions(");
    Console.Error.WriteLine("      ProtoPath = \"Contracts/Proto\",");
    Console.Error.WriteLine("      Routes = [");
    Console.Error.WriteLine("          new ProtoTypeRoute(\"requests\",  \"Request\", \"Query\"),");
    Console.Error.WriteLine("          new ProtoTypeRoute(\"responses\", \"Response\", \"Result\"),");
    Console.Error.WriteLine("          new ProtoTypeRoute(\"messages\",  \"Message\", \"Event\"),");
    Console.Error.WriteLine("      ]");
    Console.Error.WriteLine("  )]");
}

static Dictionary<string, string> ResolvePrimaryTypeNames(Assembly assembly, IEnumerable<string> fileKeys)
{
    var keySet = fileKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var type in assembly.GetTypes())
    {
        var fileKey = ProtoSchemaGenerator.ResolveFileKey(type);
        if (!keySet.Contains(fileKey) || result.ContainsKey(fileKey))
            continue;

        result[fileKey] = type.Name;
    }

    return result;
}
