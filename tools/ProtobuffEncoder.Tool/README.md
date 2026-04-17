# proto-gen

CLI tool that scans a compiled .NET assembly for `[ProtoContract]` and `[ProtoService]` types, generates `.proto` schema files, and optionally patches `.csproj` files to include the generated output.

## Installation

```shell
dotnet tool install --global ProtobuffEncoder.Tool
```

Or for a local installation scoped to a solution:

```shell
dotnet tool install ProtobuffEncoder.Tool
```

## Quick start

Build your project first, then run `proto-gen` from your project or solution directory:

```shell
dotnet build
proto-gen
```

Guided mode will discover your compiled DLL, project directory, and `.csproj` automatically.

## Usage

```
proto-gen                                    Guided mode — prompts for all inputs
proto-gen <assembly> [output-dir] [csproj]  Explicit mode
proto-gen --auto [options]                  Force guided mode with optional flags
```

### Arguments

| Argument     | Description |
|------------- |-------------|
| `assembly`   | Path to the compiled DLL containing `[ProtoContract]` / `[ProtoService]` types. |
| `output-dir` | Override base output directory. Defaults to `ProtoPath` from assembly attributes. |
| `csproj`     | `.csproj` to patch with `<Content Include="...">` entries. |

### Options

| Option       | Description |
|------------- |-------------|
| `--auto`     | Force guided/interactive mode even when arguments are provided. |
| `--dry-run`  | Preview what would be written without touching the file system. |
| `--verbose`  | Print per-file message / service / import counts. |
| `--help, -h` | Show help. |

## Configuration

Place assembly-level attributes in any `.cs` file in your project to control output:

```csharp
// Sets the root output folder for generated .proto files.
// Defaults to "Contracts/Proto" when absent.
[assembly: ProtoToolOptions(ProtoPath = "Protos")]

// Routes types by name token into subfolders.
// First match wins; types with no matching rule go directly into ProtoPath.
[assembly: ProtoRoute("requests",  "Request", "Query")]
[assembly: ProtoRoute("responses", "Response", "Result")]
[assembly: ProtoRoute("messages",  "Message",  "Event")]
[assembly: ProtoRoute("services",  "Service")]
```

### File naming

| Situation | File key |
|-----------|----------|
| Type with explicit `[ProtoContract(Name = "my_file")]` | `my_file.proto` |
| Versioned contract `[ProtoContract(version: 2)]` | `v2/<namespace>.proto` |
| Type in a namespace (`MyApp.Contracts`) | `myapp_contracts.proto` |
| Top-level type (no namespace) | `<typename>.proto` |
| `[ProtoService("BodyHelper")]` interface | `BodyHelper.proto` |

### Service inference

Methods on a `[ProtoService]` interface do **not** require `[ProtoMethod]`. When no method carries `[ProtoMethod]`, all public interface methods are included automatically and the gRPC method type is inferred from the signature:

| Signature | Inferred type |
|-----------|---------------|
| `Task<TResponse> Method(TRequest req)` | Unary |
| `IAsyncEnumerable<TResponse> Method(TRequest req)` | ServerStreaming |
| `Task<TResponse> Method(IAsyncEnumerable<TRequest> stream)` | ClientStreaming |
| `IAsyncEnumerable<TResponse> Method(IAsyncEnumerable<TRequest> stream)` | DuplexStreaming |

For fine-grained control, annotate methods explicitly with `[ProtoMethod]`.

## Examples

### Minimal setup (top-level program)

```csharp
using ProtobuffEncoder.Attributes;

[assembly: ProtoToolOptions(ProtoPath = "Protos")]
[assembly: ProtoRoute("request", "Body")]
[assembly: ProtoRoute("message", "Message")]

[ProtoContract(ImplicitFields = true)]
public class Message
{
    public int ID { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public Body Body { get; set; }
}

[ProtoContract(version: 1, FieldNumbering = FieldNumbering.Alphabetical)]
public class Body
{
    public Guid Id { get; set; }
    public string Property1 { get; set; }
    public string Property2 { get; set; }
}

[ProtoService("BodyHelper")]
public interface Helper
{
    Task<Body> GetBody(int messageId);
    Task<string[]> GetProperties(int messageId);
}
```

Run `proto-gen` after `dotnet build` — generates:

```
Protos/
  message/
    message.proto      ← Message type
  request/
    body.proto         ← Body type (in v1/ subfolder)
  BodyHelper.proto     ← Helper service
```

### Dry run

```shell
proto-gen --dry-run --verbose
```

### Explicit paths

```shell
proto-gen ./bin/Release/net8.0/MyApp.dll ./src/MyApp MyApp/MyApp.csproj
```

### Batch update all projects in a solution

Run `proto-gen` once per project — guided mode discovers all `.csproj` files from the nearest `.sln` and prompts for selection.

## How it works

1. **Load** — `Assembly.LoadFrom` on the compiled DLL; no compilation is performed.
2. **Discover** — scan all types for `[ProtoContract]` and `[ProtoService]` attributes.
3. **Group** — types are grouped into `.proto` files by namespace, or by type name for global-namespace types.
4. **Resolve imports** — cross-file type references become `import` statements.
5. **Write** — files are written under `ProtoPath`, routed into subfolders by `[assembly: ProtoRoute]` rules.
6. **Patch** — if a `.csproj` path is provided, `<Content Include="...">` entries are added.
