# CLI Tool

The `ProtobuffEncoder.Tool` is a .NET CLI tool that generates `.proto` schema files from compiled assemblies and optionally patches `.csproj` files to include the generated proto files.

## Installation

```bash
dotnet tool install --global ProtobuffEncoder.Tool
```

## Usage

```bash
protobuf-encoder <assembly-path> <proto-output-dir> [csproj-path]
```

### Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `assembly-path` | Yes | Path to the compiled .NET assembly (`.dll`) |
| `proto-output-dir` | Yes | Directory to write generated `.proto` files |
| `csproj-path` | No | If provided, appends `<Protobuf>` items to the `.csproj` |

### Example

```bash
# Generate .proto files from contracts assembly
protobuf-encoder ./bin/Release/net10.0/MyApp.Contracts.dll ./protos

# Generate and patch csproj
protobuf-encoder ./bin/Release/net10.0/MyApp.Contracts.dll ./protos ./MyApp.Server.csproj
```

## What It Does

1. **Loads the assembly** using `Assembly.LoadFrom()`
2. **Scans for types** with `[ProtoContract]` and `[ProtoService]` attributes
3. **Generates `.proto` files** via `ProtoSchemaGenerator.GenerateToDirectory()`
   - Groups types by namespace/version into separate `.proto` files
   - Generates import statements for cross-file references
   - Creates versioned subdirectories (`v1/`, `v2/`, etc.)
4. **Patches the `.csproj`** (optional) via `ProjectModifier.AppendToCsproj()`
   - Adds `<Protobuf Include="path/to/file.proto" />` items
   - Skips duplicates (won't add a file that's already referenced)
   - Creates `<ItemGroup>` if none exists, reuses existing ones

## Generated Output

For an assembly containing:

```C#
[ProtoContract(Version = 1, Name = "Order")]
public class Order { ... }

[ProtoContract(Name = "CustomerDetails")]
public class CustomerDetails { ... }

[ProtoService("OrderProcessingService")]
public interface IOrderProcessingService { ... }
```

The tool generates:

```
protos/
├── v1/
│   └── Order.proto
├── CustomerDetails.proto
└── OrderProcessingService.proto
```

## ProjectModifier

The `ProjectModifier` class handles `.csproj` file manipulation:

### Features

- **Idempotent** -- won't add duplicate entries
- **Creates `ItemGroup`** if the csproj doesn't have one
- **Reuses existing `ItemGroup`** if one exists
- **Handles subdirectory paths** correctly
- **Batch operations** -- handles multiple files in a single call
- **XML-safe** -- proper XML document manipulation via `System.Xml.Linq`

### API

```C#
ProjectModifier.AppendToCsproj(csprojPath, protoFilePaths);
```

### Example csproj After Patching

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Protobuf Include="protos\v1\Order.proto" />
    <Protobuf Include="protos\CustomerDetails.proto" />
    <Protobuf Include="protos\OrderProcessingService.proto" />
  </ItemGroup>
</Project>
```
