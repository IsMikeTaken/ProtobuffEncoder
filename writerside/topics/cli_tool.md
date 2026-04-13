# CLI Tool

`ProtobuffEncoder.Tool` is a .NET global tool that scans compiled assemblies for types decorated
with `[ProtoContract]` and `[ProtoService]`, generates `.proto` schema files with configurable
output routing, and optionally patches `.csproj` files to include the generated output.

## Installation

Install as a global tool from NuGet:

```bash
dotnet tool install --global ProtobuffEncoder.Tool
```

Or as a local (project-scoped) tool:

```bash
dotnet tool install ProtobuffEncoder.Tool
```

Or run directly from source:

```bash
dotnet run --project tools/ProtobuffEncoder.Tool -- <assembly-path> [output-dir] [csproj-path]
```

## Usage

```
proto-gen [assembly-path] [output-dir] [csproj-path] [options]
```

| Argument | Required | Description |
|----------|----------|-------------|
| `assembly-path` | No* | Path to the compiled `.dll` containing `[ProtoContract]` / `[ProtoService]` types |
| `output-dir` | No | Override the base output directory (default: from `[ProtoToolOptions]`, fallback `Contracts/Proto/`) |
| `csproj-path` | No | `.csproj` file to auto-append `<Content>` references for all generated files |

*Required unless `--auto` is used.

### Options

| Flag | Description |
|------|-------------|
| `--auto` | Guided mode: discovers assembly, project directory, and `.csproj` automatically |
| `--verbose` | Show per-file message count, service count, and import count |
| `--dry-run` | Print what would be written without touching the filesystem |
| `--help` / `-h` | Display usage information |

### Examples

```bash
# Guided — prompts for assembly selection and whether to update the project file
proto-gen --auto

# Minimal — uses [ProtoToolOptions] from the assembly (or defaults to Contracts/Proto/)
proto-gen ./bin/Release/net10.0/MyApp.Contracts.dll

# Override output directory
proto-gen ./bin/Release/net10.0/MyApp.Contracts.dll ./gen/protos

# Generate and patch the csproj
proto-gen ./bin/Release/net10.0/MyApp.Contracts.dll ./gen/protos ./MyApp.Server.csproj

# Preview what would be written without touching files
proto-gen ./bin/Release/net10.0/MyApp.Contracts.dll --dry-run --verbose
```

Sample output:

```
  Generated: Contracts/Proto/requests/myapp_contracts.proto
  Generated: Contracts/Proto/responses/myapp_contracts.proto
  Generated: Contracts/Proto/v1/OrderService.proto
  Updated:   MyApp.Server.csproj
Done. Generated 3 .proto file(s).
```

## Assembly-Level Configuration

Place `[assembly: ProtoToolOptions]` and `[assembly: ProtoRoute]` in any `.cs` file in your project
(commonly `Properties/AssemblyInfo.cs` or a dedicated `ProtoConfig.cs`).
The tool reads these at generation time — no MSBuild integration or code-generation step required.

```C#
[assembly: ProtoToolOptions(ProtoPath = "Contracts/Proto")]
[assembly: ProtoRoute("requests",  "Request", "Query")]
[assembly: ProtoRoute("responses", "Response", "Result")]
[assembly: ProtoRoute("messages",  "Message",  "Event", "Notification")]
[assembly: ProtoRoute("services",  "Service")]
```

### ProtoToolOptionsAttribute

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ProtoPath` | `string` | `"Contracts/Proto"` | Root output folder for `.proto` files, relative to the project directory |

### ProtoRouteAttribute

Each `[assembly: ProtoRoute]` maps one or more name tokens to a subfolder inside `ProtoPath`.
A type matches when its unqualified name **starts with** or **ends with** any of the tokens
(case-insensitive). Types that match no rule land directly in `ProtoPath`.
Rules are evaluated in declaration order; first match wins.

```C#
[assembly: ProtoRoute("requests", "Request", "Query")]
```

| Parameter | Description |
|-----------|-------------|
| `folder` | Subfolder relative to `ProtoPath`, e.g. `"requests"` |
| `tokens` | One or more name tokens, e.g. `"Request"`, `"Query"` |

**Routing examples** with `ProtoPath = "Contracts/Proto"`:

| Type Name | Matching Token | Output Path |
|-----------|---------------|-------------|
| `WeatherRequest` | `Request` (suffix) | `Contracts/Proto/requests/...` |
| `QueryUsers` | `Query` (prefix) | `Contracts/Proto/requests/...` |
| `OrderResponse` | `Response` (suffix) | `Contracts/Proto/responses/...` |
| `ChatMessage` | `Message` (suffix) | `Contracts/Proto/messages/...` |
| `StockEvent` | `Event` (suffix) | `Contracts/Proto/messages/...` |
| `IOrderService` | `Service` (suffix) | `Contracts/Proto/services/...` |
| `Customer` | *(no match)* | `Contracts/Proto/...` |

Version-based subdirectories (`v1/`, `v2/`) from `[ProtoContract(Version = 1)]` or
`[ProtoService("Name", Version = 1)]` are preserved *inside* the routed folder:

```
Contracts/Proto/requests/v1/order_namespace.proto
```

## How It Works

The tool follows a 4-phase pipeline to convert C# types into `.proto` schemas:

### Phase 1 — Assembly Scanning

Loads the target assembly via `Assembly.LoadFrom()` and discovers all types with:

- `[ProtoContract]` — classes, structs, and enums for protobuf serialization
- `[ProtoService]` — interfaces or classes defining gRPC service contracts

Service interfaces implemented by discovered types are auto-registered even if not directly
attributed in the scanned assembly.

### Phase 2 — Type Registry & File Grouping

Each type is assigned a **file key** that determines which `.proto` file it belongs to:

| Type | File Key Rule | Example |
|------|---------------|---------|
| `[ProtoService("OrderService")]` | `{ServiceName}.proto` | `OrderService.proto` |
| `[ProtoService("OrderService", Version = 2)]` | `v{Version}/{ServiceName}.proto` | `v2/OrderService.proto` |
| `[ProtoContract(Name = "Order")]` | `{Name}.proto` | `Order.proto` |
| `[ProtoContract(Version = 1)]` | `v{Version}/{namespace}.proto` | `v1/myapp_contracts.proto` |
| `[ProtoContract(Name = "Order", Version = 1)]` | `v{Version}/{Name}.proto` | `v1/Order.proto` |
| `[ProtoContract]` (no overrides) | `{namespace}.proto` | `myapp_contracts.proto` |

Namespace-based keys convert dots to underscores and lowercase:
`MyApp.Contracts` → `myapp_contracts.proto`.

### Phase 3 — Cross-File Import Resolution

The generator walks all message fields, oneof members, map types, and service RPC signatures to
detect references to types in other files and adds `import` statements automatically:

```
syntax = "proto3";
package MyApp.Contracts;

import "Order.proto";
import "v1/Customer.proto";

service OrderProcessingService {
  rpc PlaceOrder (PlaceOrderRequest) returns (PlaceOrderResponse);
}
```

### Phase 4 — Routing & Output

File keys are passed through the `[ProtoRoute]` rules to produce final output paths,
then written to disk. The `.csproj` patcher adds `<Content>` entries grouped by directory.

## Attribute System Reference

### ProtoContract

```C#
[ProtoContract]
public class SimpleMessage { }

[ProtoContract("OrderDetails")]           // override message/file name
public class Order { }

[ProtoContract(Version = 2)]              // output to v2/ directory
public class OrderV2 { }
```

| Property | Type | Default | Effect on Generated Schema |
|----------|------|---------|---------------------------|
| `Name` | `string?` | `null` | Overrides the proto message name and output file name |
| `Version` | `int` | `0` | Places the `.proto` file in a `v{Version}/` subdirectory |
| `ExplicitFields` | `bool` | `false` | Only `[ProtoField]`-marked properties are included |
| `IncludeBaseFields` | `bool` | `false` | Walks the inheritance chain and includes base class properties |
| `ImplicitFields` | `bool` | `false` | Auto-includes nested object properties without `[ProtoContract]` |
| `SkipDefaults` | `bool` | `true` | Skip default-valued fields (proto3 behaviour) |
| `Metadata` | `string?` | `null` | Added as a comment above the message |

### ProtoField

```C#
[ProtoContract]
public class Product
{
    [ProtoField(1)]                           public int Id { get; set; }
    [ProtoField(Name = "product_name")]       public string Name { get; set; }
    [ProtoField(IsDeprecated = true)]         public string OldSku { get; set; }
    [ProtoField(IsRequired = true)]           public decimal Price { get; set; }
    [ProtoField(IsPacked = false)]            public List<int> Tags { get; set; }
    [ProtoField(WriteDefault = true)]         public int Quantity { get; set; }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FieldNumber` | `int` | `0` (auto) | The 1-based protobuf field number |
| `Name` | `string?` | `null` | Overrides the field name in the schema |
| `WireType` | `WireType?` | `null` | Forces a specific wire type |
| `WriteDefault` | `bool` | `false` | Write even when value is the CLR default |
| `IsPacked` | `bool?` | `null` | Controls packed encoding for repeated scalars |
| `IsDeprecated` | `bool` | `false` | Adds `[deprecated = true]` annotation |
| `IsRequired` | `bool` | `false` | Library-level required check at encode time |

### ProtoService & ProtoMethod

```C#
[ProtoService("OrderProcessingService", Version = 1)]
public interface IOrderProcessingService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<OrderResponse> PlaceOrder(OrderRequest request);

    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    IAsyncEnumerable<OrderUpdate> TrackOrder(TrackRequest request, CancellationToken ct);

    [ProtoMethod(ProtoMethodType.ClientStreaming)]
    Task<BatchResult> SubmitBatch(IAsyncEnumerable<OrderRequest> stream, CancellationToken ct);

    [ProtoMethod(ProtoMethodType.DuplexStreaming)]
    IAsyncEnumerable<PriceUpdate> NegotiatePrice(IAsyncEnumerable<PriceOffer> stream, CancellationToken ct);
}
```

| `ProtoMethodType` | C# Return Type | Proto Schema |
|-------------------|----------------|--------------|
| `Unary` | `Task<TResponse>` | `rpc M (TRequest) returns (TResponse)` |
| `ServerStreaming` | `IAsyncEnumerable<TResponse>` | `rpc M (TRequest) returns (stream TResponse)` |
| `ClientStreaming` | `Task<TResponse>` | `rpc M (stream TRequest) returns (TResponse)` |
| `DuplexStreaming` | `IAsyncEnumerable<TResponse>` | `rpc M (stream TRequest) returns (stream TResponse)` |

### ProtoIgnore

```C#
[ProtoIgnore]
public string InternalToken { get; set; }   // excluded from .proto
```

### ProtoInclude

Declares derived types for polymorphic serialization. Field numbers must not collide with the
base type's own fields.

```C#
[ProtoContract]
[ProtoInclude(10, typeof(Dog))]
[ProtoInclude(11, typeof(Cat))]
public class Animal { public string Name { get; set; } }
```

### ProtoMap

Marks a `Dictionary<TKey, TValue>` property as a protobuf map field.

```C#
[ProtoMap]
public Dictionary<string, int> Stock { get; set; }
```

### ProtoOneOf

Groups properties into a `oneof` union.

```C#
[ProtoOneOf("contact")] public string? Email { get; set; }
[ProtoOneOf("contact")] public string? Phone { get; set; }
```

## Wire Type Inference

| Wire Type | CLR Types |
|-----------|-----------|
| **Varint** | `int`, `uint`, `short`, `ushort`, `byte`, `sbyte`, `bool`, `enum`, `nint`, `nuint` |
| **Fixed64** | `double`, `long`, `ulong`, `DateTime`, `TimeSpan` |
| **Fixed32** | `float` |
| **LengthDelimited** | `string`, `byte[]`, `Guid`, `decimal`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `Int128`, `UInt128`, `BigInteger`, `Complex`, `Half`, `Version`, `Uri` |

## ProjectModifier

When `csproj-path` is provided (or selected in `--auto` mode), the tool adds
`<Content Include="..." CopyToOutputDirectory="PreserveNewest" />` entries for each generated file.
Entries already present (case-insensitive) are skipped.
Files are grouped by directory into separate `<ItemGroup>` blocks.

```xml
<ItemGroup>
  <Content Include="Contracts\Proto\requests\myapp_contracts.proto">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
<ItemGroup>
  <Content Include="Contracts\Proto\responses\myapp_contracts.proto">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

## End-to-End Example

Configuration in `ProtoConfig.cs`:

```C#
[assembly: ProtoToolOptions(ProtoPath = "Contracts/Proto")]
[assembly: ProtoRoute("requests",  "Request")]
[assembly: ProtoRoute("responses", "Response")]
```

Types:

```C#
[ProtoContract(Version = 1)]
public class PlaceOrderRequest { public string ProductId { get; set; } public int Qty { get; set; } }

[ProtoContract(Version = 1)]
public class PlaceOrderResponse { public string OrderId { get; set; } public string Status { get; set; } }

[ProtoService("OrderService", Version = 1)]
public interface IOrderService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<PlaceOrderResponse> PlaceOrder(PlaceOrderRequest request);
}
```

Command:

```bash
proto-gen ./bin/Release/net10.0/MyApp.dll ./ignored MyApp.csproj --verbose
```

Output:

```
  Generated: Contracts/Proto/requests/v1/myapp_contracts.proto
             2 message(s), 0 service(s), 0 import(s)
  Generated: Contracts/Proto/responses/v1/myapp_contracts.proto
             1 message(s), 0 service(s), 0 import(s)
  Generated: Contracts/Proto/services/v1/OrderService.proto
             0 message(s), 1 service(s), 2 import(s)
  Updated:   MyApp.csproj
Done. Generated 3 .proto file(s).
```

## Guided Mode (--auto)

When no assembly path is supplied, `--auto` walks you through generation interactively:

1. Scans the current directory for compiled `.dll` files and presents a numbered list
2. Walks up the directory tree to locate the project root (first folder containing a `.csproj`)
3. Applies `[ProtoToolOptions]` / `[ProtoRoute]` configuration from the loaded assembly
4. Prompts whether to update the discovered `.csproj` with `<Content>` entries

```bash
proto-gen --auto

Select the assembly to scan:
  [1] bin/Release/net10.0/MyApp.Contracts.dll
  [2] bin/Release/net10.0/MyApp.dll
Choice [1-2] (default: 1): 1

  Generated: Contracts/Proto/requests/v1/myapp_contracts.proto
  Generated: Contracts/Proto/responses/v1/myapp_contracts.proto
  Update MyApp.Contracts.csproj with <Content> entries? [Y/n]: y
  Updated:   MyApp.Contracts.csproj
Done. Generated 2 .proto file(s).
```

## Multi-Target Support

The tool targets `net8.0`, `net9.0`, and `net10.0` and is published as a multi-target NuGet
package. `dotnet tool install` picks the best match for the installed runtime.

## Test Coverage

The `ProjectModifier` and routing logic are covered by tests in `ProtobuffEncoder.Tool.Tests`.
