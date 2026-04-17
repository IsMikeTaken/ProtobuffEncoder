using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using ProtobuffEncoder.Schema;
using ProtobuffEncoder.Tool;

internal static class Program
{
#if NET8_0_OR_GREATER
    private static readonly SearchValues<char> LineBreakChars = SearchValues.Create("\r\n");
#endif

    private const string ImportKeyword = "import ";
    private const string ServiceKeyword = "service ";
    private const string MessageKeyword = "message ";

    public static int Main(string[] args)
    {
        CliOptions options = ParseArguments(args);

        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        string assemblyPath = ResolveAssemblyPath(options);
        if (!File.Exists(assemblyPath))
        {
            Console.Error.WriteLine($"error: assembly not found: {assemblyPath}");
            return 1;
        }

        if (!TryLoadAssembly(assemblyPath, out Assembly? assembly))
            return 1;

        AssemblyToolOptions toolOptions = AssemblyToolOptions.Read(assembly!);

        string projectDir = ResolveProjectDirectory(options, assemblyPath);
        string? csprojPath = ResolveCsprojPath(options, projectDir);

        Dictionary<string, string> protoFiles = ProtoSchemaGenerator.GenerateAll(assembly!);
        if (protoFiles.Count == 0)
        {
            Console.WriteLine("No [ProtoContract] or [ProtoService] types found in the assembly.");
            return 0;
        }

        Dictionary<string, string> primaryTypeNames = ResolvePrimaryTypeNames(assembly!, protoFiles.Keys);
        List<(string RelativePath, string AbsolutePath)> written =
            WriteProtoFiles(protoFiles, primaryTypeNames, toolOptions, projectDir, options);

        if (options.DryRun)
        {
            Console.WriteLine();
            Console.WriteLine($"Dry run complete. {protoFiles.Count} file(s) would be written. No changes made.");
            return 0;
        }

        UpdateProjectFileIfRequested(csprojPath, written);

        Console.WriteLine();
        Console.WriteLine($"Done. Generated {written.Count} of {protoFiles.Count} .proto file(s).");
        return 0;
    }

    /// <summary>
    /// Parses command-line arguments in a single pass to minimize startup allocations.
    /// </summary>
    private static CliOptions ParseArguments(string[] args)
    {
        bool showHelp = false;
        bool verbose = false;
        bool dryRun = false;
        bool autoMode = args.Length == 0;

        List<string> positional = new(capacity: Math.Min(args.Length, 3));

        foreach (string arg in args)
        {
            switch (arg)
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;

                case "--verbose":
                    verbose = true;
                    break;

                case "--dry-run":
                    dryRun = true;
                    break;

                case "--auto":
                    autoMode = true;
                    break;

                default:
                    if (!arg.StartsWith("--", StringComparison.Ordinal))
                        positional.Add(arg);
                    break;
            }
        }

        return new CliOptions(
            ShowHelp: showHelp,
            Verbose: verbose,
            DryRun: dryRun,
            AutoMode: autoMode,
            PositionalArguments: positional.ToArray());
    }

    /// <summary>
    /// Resolves the assembly path from positional arguments or interactive discovery.
    /// </summary>
    private static string ResolveAssemblyPath(CliOptions options)
    {
        if (options.PositionalArguments.Length >= 1)
            return Path.GetFullPath(options.PositionalArguments[0]);

        if (options.AutoMode)
            return DiscoverAssemblyInteractive();

        Console.Error.WriteLine("error: assembly-path is required (or run without arguments for guided mode).");
        Console.Error.WriteLine();
        PrintUsage();
        Environment.Exit(1);
        return string.Empty;
    }

    /// <summary>
    /// Loads the target assembly and prints a friendly error if loading fails.
    /// </summary>
    private static bool TryLoadAssembly(string assemblyPath, out Assembly? assembly)
    {
        try
        {
            assembly = Assembly.LoadFrom(assemblyPath);
            return true;
        }
        catch (Exception ex)
        {
            assembly = null;
            Console.Error.WriteLine($"error: could not load assembly: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Resolves the project directory used as the base anchor for generated file paths.
    /// </summary>
    private static string ResolveProjectDirectory(CliOptions options, string assemblyPath)
    {
        if (options.PositionalArguments.Length >= 2)
        {
            return Path.GetDirectoryName(Path.GetFullPath(options.PositionalArguments[1]))
                   ?? Directory.GetCurrentDirectory();
        }

        if (options.AutoMode)
            return DiscoverProjectDirInteractive(assemblyPath);

        return Path.GetDirectoryName(assemblyPath) ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Resolves the optional .csproj path from positional arguments or interactive discovery.
    /// </summary>
    private static string? ResolveCsprojPath(CliOptions options, string projectDir)
    {
        if (options.PositionalArguments.Length >= 3)
            return Path.GetFullPath(options.PositionalArguments[2]);

        if (options.AutoMode)
            return DiscoverCsprojInteractive(projectDir);

        return null;
    }

    /// <summary>
    /// Writes generated .proto files, or prints the plan when running in dry-run mode.
    /// </summary>
    private static List<(string RelativePath, string AbsolutePath)> WriteProtoFiles(
        IReadOnlyDictionary<string, string> protoFiles,
        IReadOnlyDictionary<string, string> primaryTypeNames,
        AssemblyToolOptions toolOptions,
        string projectDir,
        CliOptions options)
    {
        List<(string RelativePath, string AbsolutePath)> written = new(protoFiles.Count);

        if (options.DryRun)
        {
            Console.WriteLine($"Dry run — {protoFiles.Count} file(s) would be written to {toolOptions.ProtoPath}:");
            Console.WriteLine();
        }

        foreach ((string fileKey, string content) in protoFiles.OrderBy(static kv => kv.Key))
        {
            string primaryType = primaryTypeNames.GetValueOrDefault(fileKey) ?? fileKey;
            string relativePath = toolOptions.ResolveOutputPath(fileKey, primaryType);
            string absolutePath = Path.GetFullPath(Path.Combine(projectDir, relativePath));

            if (options.DryRun)
            {
                Console.WriteLine($"  {relativePath}");
                if (options.Verbose)
                    PrintVerboseStats(content);

                continue;
            }

            string? directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(absolutePath, content);
            written.Add((relativePath, absolutePath));

            Console.WriteLine($"  Generated: {relativePath}");
            if (options.Verbose)
                PrintVerboseStats(content);
        }

        return written;
    }

    /// <summary>
    /// Updates the selected project file with generated &lt;Content&gt; items.
    /// </summary>
    private static void UpdateProjectFileIfRequested(
        string? csprojPath,
        List<(string RelativePath, string AbsolutePath)> written)
    {
        if (written.Count == 0 || string.IsNullOrEmpty(csprojPath) || !File.Exists(csprojPath))
            return;

        ProjectModifier.AppendToCsproj(csprojPath, written);
        string csprojRel = Path.GetRelativePath(Directory.GetCurrentDirectory(), csprojPath);
        Console.WriteLine($"  Updated:   {csprojRel}");
    }

    /// <summary>
    /// Resolves the "primary type name" for each proto file key so that
    /// <see cref="AssemblyToolOptions.ResolveOutputPath"/> can apply routing rules.
    /// Walks assembly types once; returns early when all keys are satisfied.
    /// </summary>
    private static Dictionary<string, string> ResolvePrimaryTypeNames(
        Assembly asm,
        IEnumerable<string> fileKeys)
    {
        HashSet<string> keySet = fileKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);

        foreach (Type type in asm.GetTypes())
        {
            string key = ProtoSchemaGenerator.ResolveFileKey(type);
            if (keySet.Contains(key) &&
                result.TryAdd(key, type.Name) &&
                result.Count == keySet.Count)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Prints a compact stat line for a single .proto file (messages, services, imports).
    /// Uses a span-based scan to avoid intermediate string allocations on .NET 8+.
    /// </summary>
    private static void PrintVerboseStats(string content)
    {
        (int messages, int services, int imports) = CountProtoDeclarations(content);
        Console.WriteLine($"             {messages} message(s), {services} service(s), {imports} import(s)");
    }

#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int Messages, int Services, int Imports) CountProtoDeclarations(string content)
    {
        int imports = 0;
        int services = 0;
        int messages = 0;

        ReadOnlySpan<char> remaining = content.AsSpan();

        while (!remaining.IsEmpty)
        {
            int newlineIndex = remaining.IndexOfAny(LineBreakChars);

            ReadOnlySpan<char> line;
            if (newlineIndex < 0)
            {
                line = remaining;
                remaining = ReadOnlySpan<char>.Empty;
            }
            else
            {
                line = remaining[..newlineIndex];

                int advance = 1;
                if (remaining[newlineIndex] == '\r' &&
                    newlineIndex + 1 < remaining.Length &&
                    remaining[newlineIndex + 1] == '\n')
                {
                    advance = 2;
                }

                remaining = remaining[(newlineIndex + advance)..];
            }

            CountDeclaration(line, ref messages, ref services, ref imports);
        }

        return (messages, services, imports);
    }
#else
    private static (int Messages, int Services, int Imports) CountProtoDeclarations(string content)
    {
        int imports = 0;
        int services = 0;
        int messages = 0;

        foreach (string line in content.Split('\n'))
        {
            CountDeclaration(line.AsSpan(), ref messages, ref services, ref imports);
        }

        return (messages, services, imports);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CountDeclaration(
        ReadOnlySpan<char> line,
        ref int messages,
        ref int services,
        ref int imports)
    {
        ReadOnlySpan<char> trimmed = line.TrimStart();

        if (trimmed.StartsWith(ImportKeyword, StringComparison.Ordinal))
        {
            imports++;
        }
        else if (trimmed.StartsWith(ServiceKeyword, StringComparison.Ordinal))
        {
            services++;
        }
        else if (trimmed.StartsWith(MessageKeyword, StringComparison.Ordinal))
        {
            messages++;
        }
    }

    /// <summary>
    /// Interactively discovers compiled DLLs under the current working directory,
    /// filters out obj/ and ref/ build artifacts, and asks the user to pick one.
    /// Falls back to the most-recently-written DLL if the user presses Enter.
    /// </summary>
    private static string DiscoverAssemblyInteractive()
    {
        string cwd = Directory.GetCurrentDirectory();
        char sep = Path.DirectorySeparatorChar;

        List<string> candidates = Directory
            .EnumerateFiles(cwd, "*.dll", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{sep}obj{sep}", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains($"{sep}ref{sep}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(10)
            .ToList();

        if (candidates.Count == 0)
        {
            Console.Error.WriteLine("error: no compiled DLLs found — build your project first.");
            Environment.Exit(1);
            return string.Empty;
        }

        Console.WriteLine("Select the assembly to scan:");
        Console.WriteLine();

        for (int i = 0; i < candidates.Count; i++)
            Console.WriteLine($"  [{i + 1}] {Path.GetRelativePath(cwd, candidates[i])}");

        Console.WriteLine();
        Console.Write($"Choice [1-{candidates.Count}] (default: 1): ");
        string? input = Console.ReadLine()?.Trim();

        int choice = 1;
        if (!string.IsNullOrEmpty(input) &&
            int.TryParse(input, out int parsed) &&
            parsed >= 1 &&
            parsed <= candidates.Count)
        {
            choice = parsed;
        }

        return Path.GetFullPath(candidates[choice - 1]);
    }

    /// <summary>
    /// Walks up the directory tree from the given assembly path until a folder
    /// containing a <c>.csproj</c> file is found. Returns the current directory
    /// as a fallback if none is found.
    /// </summary>
    private static string DiscoverProjectDirInteractive(string asmPath)
    {
        DirectoryInfo? dir = new(
            Path.GetDirectoryName(asmPath) ?? Directory.GetCurrentDirectory());

        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.csproj", SearchOption.TopDirectoryOnly).Any())
                return dir.FullName;

            dir = dir.Parent;
        }

        Console.WriteLine("  No .csproj found above the assembly — using current directory as project root.");
        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Discovers <c>.csproj</c> files in the given directory and, optionally, in
    /// the nearest solution file up the tree (for multi-project solutions).
    /// Prompts the user to choose which project(s) to update, or to skip entirely.
    /// </summary>
    private static string? DiscoverCsprojInteractive(string projectDir)
    {
        List<string> slnCandidates = FindSlnProjects(projectDir);
        List<string> localCsprojs = Directory
            .EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly)
            .ToList();

        List<string> csprojs = slnCandidates.Count > 0 ? slnCandidates : localCsprojs;

        if (csprojs.Count == 0)
        {
            Console.WriteLine("  No .csproj found — skipping project file update.");
            return null;
        }

        Console.WriteLine();

        if (csprojs.Count == 1)
        {
            string name = Path.GetFileName(csprojs[0]);
            Console.Write($"  Update '{name}' with <Content> entries for the generated files? [Y/n]: ");
            string? answer = Console.ReadLine()?.Trim().ToLowerInvariant();
            return answer is "" or "y" or "yes" ? csprojs[0] : null;
        }

        Console.WriteLine("  Which project should receive the <Content> includes?");
        Console.WriteLine("  (0 = skip, a = all listed)");
        Console.WriteLine();

        for (int i = 0; i < csprojs.Count; i++)
            Console.WriteLine($"    [{i + 1}] {Path.GetRelativePath(projectDir, csprojs[i])}");

        Console.WriteLine();
        Console.Write($"  Choice [0-{csprojs.Count} / a] (default: 0): ");
        string? input = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (input is "a" or "all")
        {
            Console.WriteLine("  Note: only the first project will be updated in this run.");
            Console.WriteLine("  Re-run proto-gen and select each project individually to update all.");
            return csprojs[0];
        }

        return !string.IsNullOrEmpty(input) &&
               int.TryParse(input, out int pick) &&
               pick >= 1 &&
               pick <= csprojs.Count
            ? csprojs[pick - 1]
            : null;
    }

    /// <summary>
    /// Walks up from <paramref name="startDir"/> looking for a <c>.sln</c> file.
    /// When one is found, parses all <c>Project(...)</c> lines to extract the
    /// referenced <c>.csproj</c> paths and returns them as absolute paths.
    /// Returns an empty list when no solution is found.
    /// </summary>
    private static List<string> FindSlnProjects(string startDir)
    {
        DirectoryInfo? dir = new(startDir);

        while (dir is not null)
        {
            FileInfo? sln = dir.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (sln is not null)
            {
                List<string> projects = [];

                foreach (string line in File.ReadLines(sln.FullName))
                {
                    int csprojIndex = line.IndexOf(".csproj\"", StringComparison.OrdinalIgnoreCase);
                    if (csprojIndex < 0)
                        continue;

                    int quoteStart = line.LastIndexOf('"', csprojIndex - 1);
                    if (quoteStart < 0)
                        continue;

                    string relative = line[(quoteStart + 1)..(csprojIndex + ".csproj".Length)];
                    string full = Path.GetFullPath(Path.Combine(dir.FullName, relative.Replace('\\', '/')));

                    if (File.Exists(full))
                        projects.Add(full);
                }

                return projects;
            }

            dir = dir.Parent;
        }

        return [];
    }

    /// <summary>
    /// Prints command-line usage with examples.
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("""
            proto-gen — Generate .proto schemas from compiled .NET assemblies.

            USAGE
              proto-gen                                    Guided mode (prompts for all inputs)
              proto-gen <assembly> [output-dir] [csproj]   Explicit mode
              proto-gen --auto [options]                   Force guided mode with optional flags

            ARGUMENTS
              assembly    Path to the compiled DLL containing [ProtoContract] / [ProtoService] types.
              output-dir  Override base output directory (default: ProtoPath from assembly attributes).
              csproj      .csproj to patch with &lt;Content Include="..."&gt; entries.

            OPTIONS
              --auto        Force guided/interactive mode even when arguments are provided.
              --dry-run     Show what would be written without touching the file system.
              --verbose     Print per-file message / service / import counts.
              --help, -h    Show this help.

            CONFIGURATION
              Place these attributes in any .cs file in your project:

                // Sets the root output folder for generated .proto files.
                // Defaults to "Contracts/Proto" when absent.
                [assembly: ProtoToolOptions(ProtoPath = "Contracts/Proto")]

                // Routes types whose name starts or ends with a token into a subfolder.
                // Multiple attributes are applied in order; first match wins.
                [assembly: ProtoRoute("requests",  "Request", "Query")]
                [assembly: ProtoRoute("responses", "Response", "Result")]
                [assembly: ProtoRoute("messages",  "Message", "Event", "Notification")]
                [assembly: ProtoRoute("services",  "Service")]

            EXAMPLES
              # Guided mode — run with no arguments inside your solution root:
              proto-gen

              # Explicit single-assembly run:
              proto-gen ./bin/Release/net8.0/MyApp.dll

              # Dry run to preview output before writing:
              proto-gen --dry-run

              # Verbose dry run showing per-file stats:
              proto-gen --dry-run --verbose

              # Explicit paths, update .csproj:
              proto-gen ./bin/Release/net8.0/MyApp.dll ./src/MyApp MyApp/MyApp.csproj
            """);
    }

    private readonly record struct CliOptions(
        bool ShowHelp,
        bool Verbose,
        bool DryRun,
        bool AutoMode,
        string[] PositionalArguments);
}


