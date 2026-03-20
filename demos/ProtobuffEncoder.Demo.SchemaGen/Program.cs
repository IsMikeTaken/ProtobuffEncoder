using System.Reflection;
using ProtobuffEncoder.Schema;

// ═══════════════════════════════════════════════════════════
// Schema Generation Demo — Auto-imports & Service Wiring
// ═══════════════════════════════════════════════════════════
//
// This demo generates .proto files from the Contracts assembly,
// showcasing:
//   1. Cross-file imports when a message references types in other files
//   2. Service definitions with auto-wrapped request/response messages
//   3. Version-based directory structure (v1/)
//   4. Common type wiring across services and messages

Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║   ProtobuffEncoder — Schema Generation Demo         ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.WriteLine();

// Load the Contracts assembly which contains our models and services
var contractsAssembly = typeof(ProtobuffEncoder.Contracts.Models.Order).Assembly;

// Generate all .proto files with auto-import resolution
Console.WriteLine("Generating .proto files from Contracts assembly...");
Console.WriteLine();

var protoFiles = ProtoSchemaGenerator.GenerateAll(contractsAssembly);

foreach (var (filename, content) in protoFiles.OrderBy(kv => kv.Key))
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"═══ {filename} ═══");
    Console.ResetColor();
    Console.WriteLine(content);
    Console.WriteLine();
}

Console.WriteLine($"Total: {protoFiles.Count} .proto file(s) generated.");
Console.WriteLine();

// Also demonstrate writing to a directory
var outputDir = Path.Combine(Path.GetTempPath(), "ProtobufSchemaDemo");
if (Directory.Exists(outputDir))
    Directory.Delete(outputDir, true);

var paths = ProtoSchemaGenerator.GenerateToDirectory(contractsAssembly, outputDir);

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"Written to disk: {outputDir}");
Console.ResetColor();

foreach (var path in paths)
{
    Console.WriteLine($"  → {Path.GetRelativePath(outputDir, path)}");
}

// Cleanup
Directory.Delete(outputDir, true);

Console.WriteLine();
Console.WriteLine("Done. All .proto files generated with cross-file imports and service wiring.");
