using System.Reflection;
using ProtobuffEncoder.Schema;
using ProtobuffEncoder.Tool;

// ── Argument parsing ──────────────────────────────────────────────────────────

if (args.Contains("--help") || args.Contains("-h"))
{
    PrintUsage();
    return 0;
}

bool verbose  = args.Contains("--verbose");
bool dryRun   = args.Contains("--dry-run");
bool autoMode = args.Contains("--auto");

string[] positional = args
    .Where(static a => !a.StartsWith("--") && a != "-h")
    .ToArray();

// ── Resolve assembly path ─────────────────────────────────────────────────────

string assemblyPath;

if (positional.Length >= 1)
{
    assemblyPath = Path.GetFullPath(positional[0]);
}
else if (autoMode)
{
    assemblyPath = DiscoverAssemblyInteractive();
}
else
{
    Console.Error.WriteLine("error: assembly-path is required (or use --auto for guided mode)");
    Console.Error.WriteLine();
    PrintUsage();
    return 1;
}

if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"error: assembly not found: {assemblyPath}");
    return 1;
}

// ── Load assembly and configuration ──────────────────────────────────────────

Assembly assembly;
try
{
    assembly = Assembly.LoadFrom(assemblyPath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"error: could not load assembly: {ex.Message}");
    return 1;
}

AssemblyToolOptions toolOptions = AssemblyToolOptions.Read(assembly);

// ── Resolve project dir ───────────────────────────────────────────────────────

string projectDir;

if (positional.Length >= 2)
{
    // output-dir is provided; treat its parent as the project root
    projectDir = Path.GetDirectoryName(Path.GetFullPath(positional[1]))
                 ?? Directory.GetCurrentDirectory();
}
else if (autoMode)
{
    projectDir = DiscoverProjectDirInteractive(assemblyPath);
}
else
{
    projectDir = Path.GetDirectoryName(assemblyPath) ?? Directory.GetCurrentDirectory();
}

// ── Resolve csproj path ───────────────────────────────────────────────────────

string? csprojPath = positional.Length >= 3
    ? Path.GetFullPath(positional[2])
    : autoMode
        ? DiscoverCsprojInteractive(projectDir)
        : null;

// ── Generate ──────────────────────────────────────────────────────────────────

var protoFiles = ProtoSchemaGenerator.GenerateAll(assembly);

if (protoFiles.Count == 0)
{
    Console.WriteLine("No [ProtoContract] or [ProtoService] types found.");
    return 0;
}

var primaryTypeNames = ResolvePrimaryTypeNames(assembly, protoFiles.Keys);
var written = new List<(string RelativePath, string AbsolutePath)>();

foreach (var (fileKey, content) in protoFiles.OrderBy(static kv => kv.Key))
{
    string primaryType = primaryTypeNames.GetValueOrDefault(fileKey) ?? fileKey;
    string relPath     = toolOptions.ResolveOutputPath(fileKey, primaryType);
    string absPath     = Path.GetFullPath(Path.Combine(projectDir, relPath));

    if (dryRun)
    {
        Console.WriteLine($"  [dry-run] would write: {relPath}");
        if (verbose) PrintVerbose(content);
        continue;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
    File.WriteAllText(absPath, content);
    written.Add((relPath, absPath));
    Console.WriteLine($"  Generated: {relPath}");
    if (verbose) PrintVerbose(content);
}

if (!dryRun && written.Count > 0 && csprojPath is not null && File.Exists(csprojPath))
{
    ProjectModifier.AppendToCsproj(csprojPath, written);
    string csprojRel = Path.GetRelativePath(Directory.GetCurrentDirectory(), csprojPath);
    Console.WriteLine($"  Updated:   {csprojRel}");
}

Console.WriteLine($"Done. {(dryRun ? "Would generate" : "Generated")} {protoFiles.Count} .proto file(s).");
return 0;

// ── Local helpers ─────────────────────────────────────────────────────────────

// Non-static: references ProtoSchemaGenerator from the top-level using directive.
Dictionary<string, string> ResolvePrimaryTypeNames(Assembly asm, IEnumerable<string> fileKeys)
{
    var keySet = fileKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (Type type in asm.GetTypes())
    {
        string key = ProtoSchemaGenerator.ResolveFileKey(type);
        if (keySet.Contains(key) && result.TryAdd(key, type.Name) && result.Count == keySet.Count)
            break;
    }

    return result;
}

// Static helpers that don't reference top-level usings or captured variables.

static void PrintVerbose(string content)
{
    int imports = 0, services = 0, messages = 0;
    foreach (string line in content.Split('\n'))
    {
        ReadOnlySpan<char> trimmed = line.AsSpan().TrimStart();
        if      (trimmed.StartsWith("import ",  StringComparison.Ordinal)) imports++;
        else if (trimmed.StartsWith("service ", StringComparison.Ordinal)) services++;
        else if (trimmed.StartsWith("message ", StringComparison.Ordinal)) messages++;
    }
    Console.WriteLine($"             {messages} message(s), {services} service(s), {imports} import(s)");
}

static string DiscoverAssemblyInteractive()
{
    string cwd = Directory.GetCurrentDirectory();
    char sep = Path.DirectorySeparatorChar;

    var candidates = Directory
        .EnumerateFiles(cwd, "*.dll", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}ref{sep}"))
        .OrderByDescending(File.GetLastWriteTime)
        .Take(10)
        .ToList();

    if (candidates.Count == 0)
    {
        Console.Error.WriteLine("error: no compiled DLLs found — build your project first.");
        Environment.Exit(1);
    }

    Console.WriteLine("Select the assembly to scan:");
    for (int i = 0; i < candidates.Count; i++)
        Console.WriteLine($"  [{i + 1}] {Path.GetRelativePath(cwd, candidates[i])}");

    Console.Write($"Choice [1-{candidates.Count}] (default: 1): ");
    string? input = Console.ReadLine()?.Trim();

    int choice = 1;
    if (!string.IsNullOrEmpty(input) &&
        int.TryParse(input, out int parsed) &&
        parsed >= 1 && parsed <= candidates.Count)
    {
        choice = parsed;
    }

    return Path.GetFullPath(candidates[choice - 1]);
}

static string DiscoverProjectDirInteractive(string asmPath)
{
    var dir = new DirectoryInfo(
        Path.GetDirectoryName(asmPath) ?? Directory.GetCurrentDirectory());

    while (dir is not null)
    {
        if (dir.EnumerateFiles("*.csproj", SearchOption.TopDirectoryOnly).Any())
            return dir.FullName;
        dir = dir.Parent;
    }

    Console.WriteLine("  No .csproj found above assembly — using current directory as project root.");
    return Directory.GetCurrentDirectory();
}

static string? DiscoverCsprojInteractive(string projectDir)
{
    var csprojs = Directory
        .EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly)
        .ToList();

    if (csprojs.Count == 0)
    {
        Console.WriteLine("  No .csproj found — skipping project file update.");
        return null;
    }

    if (csprojs.Count == 1)
    {
        Console.Write($"  Update {Path.GetFileName(csprojs[0])} with <Content> entries? [Y/n]: ");
        string? answer = Console.ReadLine()?.Trim().ToLowerInvariant();
        return answer is "" or "y" or "yes" ? csprojs[0] : null;
    }

    Console.WriteLine("  Multiple .csproj files found (0 = skip):");
    for (int i = 0; i < csprojs.Count; i++)
        Console.WriteLine($"    [{i + 1}] {Path.GetRelativePath(projectDir, csprojs[i])}");

    Console.Write($"  Choice [0-{csprojs.Count}] (default: 0): ");
    string? inp = Console.ReadLine()?.Trim();

    return !string.IsNullOrEmpty(inp) &&
           int.TryParse(inp, out int pick) &&
           pick >= 1 && pick <= csprojs.Count
        ? csprojs[pick - 1]
        : null;
}

static void PrintUsage()
{
    Console.WriteLine("Usage: proto-gen [assembly-path] [output-dir] [csproj-path] [options]");
    Console.WriteLine();
    Console.WriteLine("  assembly-path   Compiled DLL with [ProtoContract] / [ProtoService] types");
    Console.WriteLine("  output-dir      Override base output directory (default: ProtoPath from assembly)");
    Console.WriteLine("  csproj-path     .csproj to update with <Content Include=...> entries");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --auto          Guided mode: discovers assembly, project dir, and csproj automatically");
    Console.WriteLine("  --verbose       Show per-file message / service / import counts");
    Console.WriteLine("  --dry-run       Print what would be written without touching the file system");
    Console.WriteLine("  --help, -h      Show this help");
    Console.WriteLine();
    Console.WriteLine("Configuration (place in any .cs file in the target project):");
    Console.WriteLine();
    Console.WriteLine("  [assembly: ProtoToolOptions(ProtoPath = \"Contracts/Proto\")]");
    Console.WriteLine("  [assembly: ProtoRoute(\"requests\",  \"Request\", \"Query\")]");
    Console.WriteLine("  [assembly: ProtoRoute(\"responses\", \"Response\", \"Result\")]");
    Console.WriteLine("  [assembly: ProtoRoute(\"messages\",  \"Message\",  \"Event\", \"Notification\")]");
    Console.WriteLine("  [assembly: ProtoRoute(\"services\",  \"Service\")]");
    Console.WriteLine();
    Console.WriteLine("  ProtoPath defaults to \"Contracts/Proto\". Unmatched types land in ProtoPath.");
}
