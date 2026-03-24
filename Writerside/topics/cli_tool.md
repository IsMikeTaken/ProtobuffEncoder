# CLI Tool

The `ProtobuffEncoder.Tool` is a .NET CLI that scans compiled assemblies for types decorated with `[ProtoContract]` and `[ProtoService]`, generates `.proto` schema files, and optionally patches `.csproj` files to include the generated output.

## Installation

```bash
dotnet tool install --global ProtobuffEncoder.Tool
```

Or run directly from source:

```bash
dotnet run --project tools/ProtobuffEncoder.Tool -- <assembly-path> <proto-output-dir> [csproj-path] [--verbose]
```

## Usage

```
ProtobuffEncoder.Tool <assembly-path> <proto-output-dir> [csproj-path] [--verbose]
```

| Argument | Required | Description |
|----------|----------|-------------|
| `assembly-path` | Yes | Path to the compiled `.dll` containing `[ProtoContract]` / `[ProtoService]` types |
| `proto-output-dir` | Yes | Directory to write generated `.proto` files |
| `csproj-path` | No | `.csproj` file to auto-append proto file references |
| `--verbose` | No | Show per-file message counts, service counts, and import relationships |
| `--help` / `-h` | No | Display usage information and feature summary |

### Examples

```bash
# Generate .proto files from a contracts assembly
protobuf-encoder ./bin/Release/net10.0/MyApp.Contracts.dll ./protos

# Generate and patch the csproj
protobuf-encoder ./bin/Release/net10.0/MyApp.Contracts.dll ./protos ./MyApp.Server.csproj

# Verbose output showing import/service details
protobuf-encoder ./bin/Release/net10.0/MyApp.Contracts.dll ./protos --verbose
```

Sample output:

```
  Generated: protobuffencoder_contracts.proto
  Generated: v1/Order.proto
  Generated: OrderProcessingService.proto

  OrderProcessingService.proto: 0 message(s), 1 service(s), 2 import(s)

  Updated csproj: ./MyApp.Server.csproj
Done. Generated 3 .proto file(s) in ./protos
```

## How It Works

The tool follows a 4-phase pipeline to convert C# types into `.proto` schemas:

### Phase 1 — Assembly Scanning

Loads the target assembly via `Assembly.LoadFrom()` and discovers all types with:

- **`[ProtoContract]`** — classes, structs, and enums marked for protobuf serialization
- **`[ProtoService]`** — interfaces or classes defining gRPC service contracts

Additionally, service interfaces implemented by discovered types are auto-registered even if not directly attributed in the scanned assembly.

### Phase 2 — Type Registry & File Grouping

Each discovered type is assigned a **file key** that determines which `.proto` file it belongs to:

| Type | File Key Rule | Example |
|------|---------------|---------|
| `[ProtoService("OrderService")]` | `{ServiceName}.proto` | `OrderService.proto` |
| `[ProtoService("OrderService", Version = 2)]` | `v{Version}/{ServiceName}.proto` | `v2/OrderService.proto` |
| `[ProtoContract(Name = "Order")]` | `{Name}.proto` | `Order.proto` |
| `[ProtoContract(Version = 1)]` | `v{Version}/{namespace}.proto` | `v1/myapp_contracts.proto` |
| `[ProtoContract(Name = "Order", Version = 1)]` | `v{Version}/{Name}.proto` | `v1/Order.proto` |
| `[ProtoContract]` (no overrides) | `{namespace}.proto` | `myapp_contracts.proto` |

Namespace-based keys convert dots to underscores and lowercase: `MyApp.Contracts` → `myapp_contracts.proto`.

Types sharing the same file key are grouped into a single `.proto` file.

### Phase 3 — Cross-File Import Resolution

The generator walks all message fields, oneof members, map key/value types, and service RPC signatures to detect references to types defined in other files. Import statements are automatically added:

```
syntax = "proto3";
package MyApp.Contracts;

import "Order.proto";          // auto-resolved: OrderLine references Order
import "v1/Customer.proto";   // auto-resolved: OrderService.GetCustomer returns Customer

service OrderProcessingService {
  rpc PlaceOrder (PlaceOrderRequest) returns (PlaceOrderResponse);
}
```

Import resolution covers:

- Message field types (including nested messages)
- Map key and value types
- OneOf member types
- Service RPC request and response types
- Recursive nested message analysis

### Phase 4 — Rendering & Output

Each `ProtoFile` model is rendered to proto3 syntax and written to disk. Versioned types create subdirectory structures automatically.

## Attribute System Reference

The tool processes the full attribute system to generate accurate `.proto` schemas. Every attribute and property below is recognized during schema generation.

### ProtoContract

Marks a class, struct, or enum for protobuf serialization. This is the primary attribute the tool scans for.

```C#
[ProtoContract]
public class SimpleMessage { ... }

[ProtoContract("OrderDetails")]           // override message/file name
public class Order { ... }

[ProtoContract(Version = 2)]              // output to v2/ directory
public class OrderV2 { ... }
```

| Property | Type | Default | Effect on Generated Schema |
|----------|------|---------|---------------------------|
| `Name` | `string?` | `null` (uses class name) | Overrides the proto message name and output file name |
| `Version` | `int` | `0` | Places the `.proto` file in a `v{Version}/` subdirectory |
| `ExplicitFields` | `bool` | `false` | When `true`, only properties with `[ProtoField]` are included in the schema |
| `IncludeBaseFields` | `bool` | `false` | When `true`, walks the inheritance chain and includes base class properties |
| `ImplicitFields` | `bool` | `false` | When `true`, nested object properties without `[ProtoContract]` are auto-included |
| `SkipDefaults` | `bool` | `true` | Controls whether default-valued fields are skipped (proto3 behavior) |
| `Metadata` | `string?` | `null` | Added as a comment above the message in the generated `.proto` |

Targets: `class`, `struct`, `enum` — with `Inherited = true`.

### ProtoField

Overrides field-level metadata for properties. Without this attribute, fields are auto-assigned numbers based on declaration order.

```C#
[ProtoContract]
public class Product
{
    [ProtoField(1)]                              // explicit field number
    public int Id { get; set; }

    [ProtoField(Name = "product_name")]           // override proto field name
    public string Name { get; set; }

    [ProtoField(IsDeprecated = true)]             // marks deprecated in schema
    public string OldSku { get; set; }

    [ProtoField(IsRequired = true)]               // library-level required check
    public decimal Price { get; set; }

    [ProtoField(IsPacked = false)]                // disable packed encoding
    public List<int> Tags { get; set; }

    [ProtoField(WriteDefault = true)]             // force write even if default
    public int Quantity { get; set; }
}
```

| Property | Type | Default | Effect on Generated Schema |
|----------|------|---------|---------------------------|
| `FieldNumber` | `int` | `0` (auto-assigned) | The 1-based protobuf field number |
| `Name` | `string?` | `null` (uses property name) | Overrides the field name in the `.proto` schema |
| `WireType` | `WireType?` | `null` (auto-inferred) | Forces a specific wire type instead of CLR type inference |
| `WriteDefault` | `bool` | `false` | Forces field to be written even with default values |
| `IsPacked` | `bool?` | `null` (proto3 default: packed) | Controls packed encoding for repeated scalar fields |
| `IsDeprecated` | `bool` | `false` | Adds `[deprecated = true]` annotation in the schema |
| `IsRequired` | `bool` | `false` | Enforces non-default value at the library level (proto3 has no `required` keyword) |

Targets: `Property` — with `Inherited = true`.

Generated schema for the example above:

```
message Product {
  int32 Id = 1;
  string product_name = 2;
  string OldSku = 3 [deprecated = true];
  string Price = 4;
  repeated int32 Tags = 5;
  int32 Quantity = 6;
}
```

### ProtoService

Marks an interface or class as a gRPC service definition. Methods decorated with `[ProtoMethod]` become RPC operations.

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

| Property | Type | Default | Effect on Generated Schema |
|----------|------|---------|---------------------------|
| `ServiceName` | `string` | *(required, constructor)* | The gRPC service identifier used in routes (`/ServiceName/MethodName`) |
| `Version` | `int` | `0` | Places the service `.proto` in a `v{Version}/` subdirectory |
| `Metadata` | `string?` | `null` | Added as a comment above the service definition |

Targets: `interface`, `class` — with `Inherited = true`.

### ProtoMethod

Marks a method on a `[ProtoService]` interface as an RPC operation.

```C#
[ProtoMethod(ProtoMethodType.Unary, Name = "GetOrder")]
Task<OrderResponse> FetchOrder(OrderRequest request);
```

| Property | Type | Default | Effect on Generated Schema |
|----------|------|---------|---------------------------|
| `MethodType` | `ProtoMethodType` | *(required, constructor)* | The gRPC method type |
| `Name` | `string?` | `null` (uses C# method name) | Overrides the RPC method name in the `.proto` |

**ProtoMethodType enum:**

| Value | C# Signature Pattern | Proto Schema |
|-------|----------------------|--------------|
| `Unary` | `Task<TResponse> Method(TRequest)` | `rpc Method (TRequest) returns (TResponse)` |
| `ServerStreaming` | `IAsyncEnumerable<TResponse> Method(TRequest, CancellationToken)` | `rpc Method (TRequest) returns (stream TResponse)` |
| `ClientStreaming` | `Task<TResponse> Method(IAsyncEnumerable<TRequest>, CancellationToken)` | `rpc Method (stream TRequest) returns (TResponse)` |
| `DuplexStreaming` | `IAsyncEnumerable<TResponse> Method(IAsyncEnumerable<TRequest>, CancellationToken)` | `rpc Method (stream TRequest) returns (stream TResponse)` |

Generated schema for the service example above:

```
syntax = "proto3";
package MyApp.Contracts;

// Service metadata comment (if Metadata is set)
service OrderProcessingService {
  rpc PlaceOrder (OrderRequest) returns (OrderResponse);
  rpc TrackOrder (TrackRequest) returns (stream OrderUpdate);
  rpc SubmitBatch (stream OrderRequest) returns (BatchResult);
  rpc NegotiatePrice (stream PriceOffer) returns (stream PriceUpdate);
}
```

### ProtoIgnore

Excludes a property from serialization and schema generation.

```C#
[ProtoContract]
public class User
{
    public string Name { get; set; }

    [ProtoIgnore]
    public string InternalToken { get; set; }   // excluded from .proto
}
```

Targets: `Property` — with `Inherited = true`.

### ProtoInclude

Declares derived types for polymorphic serialization. Each derived type is encoded as a nested message at the specified field number.

```C#
[ProtoContract]
[ProtoInclude(10, typeof(Dog))]
[ProtoInclude(11, typeof(Cat))]
public class Animal
{
    public string Name { get; set; }
}

[ProtoContract]
public class Dog : Animal
{
    public string Breed { get; set; }
}

[ProtoContract]
public class Cat : Animal
{
    public bool Indoor { get; set; }
}
```

| Property | Type | Description |
|----------|------|-------------|
| `FieldNumber` | `int` | The field number for the derived type's nested message (must not collide with base type fields) |
| `DerivedType` | `Type` | The derived CLR type |

Targets: `class` — `AllowMultiple = true`, `Inherited = false`.

Generated schema:

```
message Animal {
  string Name = 1;
  Dog dog = 10;
  Cat cat = 11;
}

message Dog {
  string Breed = 1;
}

message Cat {
  bool Indoor = 1;
}
```

### ProtoMap

Marks a `Dictionary<TKey, TValue>` property as a protobuf map field. Without this attribute, dictionaries are not serialized.

```C#
[ProtoContract]
public class Inventory
{
    [ProtoMap]
    public Dictionary<string, int> Stock { get; set; }

    [ProtoMap(KeyType = "int32", ValueType = "string")]   // explicit type override
    public Dictionary<int, string> Labels { get; set; }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `KeyType` | `string?` | `null` (inferred from CLR key type) | Override the proto key type name |
| `ValueType` | `string?` | `null` (inferred from CLR value type) | Override the proto value type name |

Targets: `Property` — with `Inherited = true`.

Generated schema:

```
message Inventory {
  map<string, int32> Stock = 1;
  map<int32, string> Labels = 2;
}
```

### ProtoOneOf

Groups properties into a protobuf `oneof` union. At most one property in a group should have a non-default value. During encoding, only the first non-default property in the group is written.

```C#
[ProtoContract]
public class ContactInfo
{
    [ProtoOneOf("contact")]
    public string? Email { get; set; }

    [ProtoOneOf("contact")]
    public string? Phone { get; set; }

    [ProtoOneOf("contact")]
    public string? Address { get; set; }
}
```

| Property | Type | Description |
|----------|------|-------------|
| `GroupName` | `string` | The `oneof` group identifier — all properties sharing the same name are grouped |

Targets: `Property` — with `Inherited = true`.

Generated schema:

```
message ContactInfo {
  oneof contact {
    string Email = 1;
    string Phone = 2;
    string Address = 3;
  }
}
```

## Wire Type Inference

The `ContractResolver` automatically infers protobuf wire types from CLR types. The tool uses this mapping when generating field type names in `.proto` schemas:

| Wire Type | CLR Types |
|-----------|-----------|
| **Varint** (`0`) | `int`, `uint`, `short`, `ushort`, `byte`, `sbyte`, `bool`, `enum`, `nint`, `nuint` |
| **Fixed64** (`1`) | `double`, `long`, `ulong`, `DateTime`, `TimeSpan` |
| **Fixed32** (`5`) | `float` |
| **LengthDelimited** (`2`) | `string`, `byte[]`, `Guid`, `decimal`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `Int128`, `UInt128`, `BigInteger`, `Complex`, `Half`, `Version`, `Uri` |

### Supported CLR Types (30+)

The full set of CLR types recognized by the schema generator:

| Category | Types |
|----------|-------|
| **Integers** | `int`, `uint`, `long`, `ulong`, `short`, `ushort`, `byte`, `sbyte`, `nint`, `nuint` |
| **Floating-point** | `float`, `double`, `Half`, `decimal` |
| **Boolean** | `bool` |
| **Text** | `string` |
| **Binary** | `byte[]` |
| **Date/Time** | `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly` |
| **Identifiers** | `Guid`, `Uri`, `Version` |
| **Large numbers** | `Int128`, `UInt128`, `BigInteger`, `Complex` |
| **Collections** | `T[]`, `List<T>`, `IList<T>`, `ICollection<T>`, `IEnumerable<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, `HashSet<T>`, `ISet<T>` |
| **Dictionaries** | `Dictionary<K,V>`, `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>` |
| **Enums** | Any `enum` type |
| **Nested types** | Any `[ProtoContract]` class or struct |

## ProjectModifier

The `ProjectModifier` class handles `.csproj` file manipulation when the optional `csproj-path` argument is provided.

### API

```C#
public static class ProjectModifier
{
    public static void AppendToCsproj(string csprojPath, string outputDir, List<string> generatedPaths);
}
```

### Behavior

| Feature | Description |
|---------|-------------|
| **Idempotent** | Skips files already referenced (case-insensitive match against existing `Include` attributes) |
| **ItemGroup reuse** | Finds existing `ItemGroup` containing `.proto` `<Content>` or `<None>` elements and appends to it |
| **ItemGroup creation** | Creates a new `<ItemGroup>` with comment `<!-- Auto-generated proto schemas -->` if none exists |
| **Relative paths** | Converts absolute paths to project-relative paths with backslash separators |
| **Copy metadata** | Adds `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` to each entry |
| **No-op on empty** | Does not modify the file when the generated paths list is empty |
| **XML-safe** | Uses `System.Xml.Linq` (`XDocument`) for proper XML manipulation |

### Generated csproj Entry

```xml
<ItemGroup>
  <!-- Auto-generated proto schemas -->
  <Content Include="protos\v1\Order.proto">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="protos\CustomerDetails.proto">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="protos\OrderProcessingService.proto">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

## Schema Model

The tool generates an intermediate model before rendering to `.proto` syntax. Understanding this model helps when extending or debugging the generator.

```
ProtoFile
├── Syntax: "proto3"
├── Package: "MyApp.Contracts"
├── FilePath: "v1/Order.proto"
├── Imports: ["Customer.proto", "Common.proto"]
├── Messages[]
│   └── ProtoMessageDef
│       ├── Name, SourceType, Metadata
│       ├── Fields[] → ProtoFieldDef
│       │   ├── Name, FieldNumber, TypeName
│       │   ├── IsRepeated, IsOptional, IsDeprecated, IsMap
│       │   ├── MapKeyType, MapValueType
│       │   └── OneOfGroup
│       ├── NestedMessages[] → ProtoMessageDef (recursive)
│       ├── NestedEnums[] → ProtoEnumDef
│       └── OneOfs[] → ProtoOneOfDef
│           └── Fields[] → ProtoFieldDef
├── Enums[]
│   └── ProtoEnumDef
│       └── Values[] → ProtoEnumValue { Name, Number }
└── Services[]
    └── ProtoServiceDef
        ├── Name, SourceType, Metadata
        └── Methods[] → ProtoRpcDef
            ├── Name, MethodType
            ├── RequestTypeName
            └── ResponseTypeName
```

## End-to-End Example

Given this C# assembly:

```C#
// Models
[ProtoContract(Name = "Order", Version = 1, Metadata = "Core order aggregate")]
public class Order
{
    [ProtoField(1)]
    public int Id { get; set; }

    [ProtoField(2, Name = "customer_name")]
    public string CustomerName { get; set; }

    [ProtoField(3)]
    public List<OrderLine> Lines { get; set; }

    [ProtoMap]
    [ProtoField(4)]
    public Dictionary<string, string> Tags { get; set; }

    [ProtoIgnore]
    public string InternalNotes { get; set; }
}

[ProtoContract(Name = "OrderLine", Version = 1)]
public class OrderLine
{
    public string ProductId { get; set; }
    public int Quantity { get; set; }

    [ProtoField(IsDeprecated = true)]
    public decimal OldPrice { get; set; }
}

public enum OrderStatus
{
    Unknown = 0,
    Pending = 1,
    Confirmed = 2,
    Shipped = 3
}

[ProtoContract]
[ProtoInclude(10, typeof(PriorityOrder))]
public class BaseOrder
{
    public int Id { get; set; }
}

[ProtoContract]
public class PriorityOrder : BaseOrder
{
    public int Priority { get; set; }
}

// Service
[ProtoService("OrderService", Version = 1, Metadata = "Order management API")]
public interface IOrderService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<Order> GetOrder(GetOrderRequest request);

    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    IAsyncEnumerable<Order> ListOrders(ListOrdersRequest request, CancellationToken ct);

    [ProtoMethod(ProtoMethodType.DuplexStreaming, Name = "SyncOrders")]
    IAsyncEnumerable<Order> Sync(IAsyncEnumerable<Order> stream, CancellationToken ct);
}
```

Running:

```bash
protobuf-encoder ./bin/Release/net10.0/MyApp.dll ./protos ./MyApp.csproj --verbose
```

Generates:

```
protos/
├── v1/
│   ├── Order.proto
│   ├── OrderLine.proto
│   └── OrderService.proto
├── myapp_contracts.proto
```

`v1/Order.proto`:
```
syntax = "proto3";
package MyApp.Contracts;

import "v1/OrderLine.proto";

// Core order aggregate
message Order {
  int32 Id = 1;
  string customer_name = 2;
  repeated OrderLine Lines = 3;
  map<string, string> Tags = 4;
}

enum OrderStatus {
  Unknown = 0;
  Pending = 1;
  Confirmed = 2;
  Shipped = 3;
}
```

`v1/OrderService.proto`:
```
syntax = "proto3";
package MyApp.Contracts;

import "v1/Order.proto";

// Order management API
service OrderService {
  rpc GetOrder (GetOrderRequest) returns (Order);
  rpc ListOrders (ListOrdersRequest) returns (stream Order);
  rpc SyncOrders (stream Order) returns (stream Order);
}
```

## Multi-Target Support

The tool project targets `net10.0`, `net9.0`, and `net8.0`. When invoked via MSBuild targets, it uses the same runtime as the host project.

```xml
<PropertyGroup>
  <TargetFrameworks>net10.0;net9.0;net8.0</TargetFrameworks>
  <OutputType>Exe</OutputType>
</PropertyGroup>
```

## Test Coverage

The `ProjectModifier` is covered by 12 tests in `ProtobuffEncoder.Tool.Tests` following the FIRST-U test patterns:

| Pattern | Tests | Description |
|---------|-------|-------------|
| **Simple-Test** | 2 | Basic append and Content element creation |
| **Collection-Constraint** | 2 | Duplicate prevention for `<Content>` and `<None>` elements |
| **Collection-Order** | 2 | Multiple files added, mixed new/existing handling |
| **Constraint-Data** | 1 | Empty list produces no modification |
| **Process-Rule** | 2 | ItemGroup creation and reuse |
| **Bit-Error-Simulation** | 1 | Subdirectory relative path handling |
| **Signalled** | 1 | Sequential calls all persist |
| **Performance** | 1 | 100 files complete in under 2 seconds |

