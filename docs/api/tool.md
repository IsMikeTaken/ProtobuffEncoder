# CLI Tool

`ProtobuffEncoder.Tool` is a CLI that scans compiled .NET assemblies for `[ProtoContract]` types and generates `.proto` schema files.

## Usage

```bash
dotnet run --project tools/ProtobuffEncoder.Tool -- <assembly-path> <proto-output-dir> [csproj-path]
```

| Argument | Required | Description |
|----------|----------|-------------|
| `assembly-path` | Yes | Path to the compiled DLL containing `[ProtoContract]` types |
| `proto-output-dir` | Yes | Directory to write generated `.proto` files |
| `csproj-path` | No | `.csproj` file to auto-append proto file references |

### Example

```bash
dotnet run --project tools/ProtobuffEncoder.Tool -- \
  "src/ProtobuffEncoder.Contracts/bin/Debug/net10.0/ProtobuffEncoder.Contracts.dll" \
  "src/ProtobuffEncoder.Contracts/protos" \
  "src/ProtobuffEncoder.Contracts/ProtobuffEncoder.Contracts.csproj"
```

Output:
```
  Generated: src/ProtobuffEncoder.Contracts/protos/protobuffencoder_contracts.proto
  Added to csproj: protos\protobuffencoder_contracts.proto
Done. Generated 1 .proto file(s) in src/ProtobuffEncoder.Contracts/protos
```

## What it does

1. **Loads** the target assembly via `Assembly.LoadFrom`
2. **Scans** for all types with `[ProtoContract]`
3. **Generates** `.proto` files (one per namespace) using `ProtoSchemaGenerator.GenerateToDirectory`
4. **Auto-appends** `<Content>` entries to the `.csproj` (if provided) with `CopyToOutputDirectory=PreserveNewest`

## csproj auto-update

When a `csproj-path` is provided, the tool:
- Finds or creates an `<ItemGroup>` for proto content
- Checks for existing includes to avoid duplicates
- Appends new `<Content Include="protos\file.proto">` entries
- Saves the modified `.csproj`

Example of what gets added:

```xml
<ItemGroup>
  <!-- Auto-generated proto schemas -->
  <Content Include="protos\protobuffencoder_contracts.proto">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

## MSBuild Integration

Instead of running the tool manually, import the MSBuild targets to auto-generate on every build:

```xml
<Import Project="..\..\src\ProtobuffEncoder\build\ProtobuffEncoder.targets" />
```

### Configuration

```xml
<PropertyGroup>
  <!-- Output directory for .proto files (default: protos) -->
  <ProtoOutputDir>protos</ProtoOutputDir>

  <!-- Enable/disable auto-generation (default: true) -->
  <GenerateProtoOnBuild>true</GenerateProtoOnBuild>
</PropertyGroup>
```

The target runs `AfterTargets="Build"` and invokes the tool with the current project's output assembly, proto directory, and csproj path.

## Multi-target Support

The tool targets `net10.0`, `net9.0`, and `net8.0`. When building via MSBuild targets, it uses the same framework as the host project.
