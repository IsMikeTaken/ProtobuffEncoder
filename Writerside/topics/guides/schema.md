# Schema Generation & Decoding

ProtobuffEncoder can auto-generate `.proto` schema files from your C# types, and decode protobuf binary using only those schemas — no C# types needed on the receiving end.

This enables the **shared contract** pattern: define types in C#, generate `.proto` files, and any consumer (even one with zero knowledge of your C# classes) can decode the messages.

## Generating .proto Schemas

### Programmatic

```C#
using ProtobuffEncoder.Schema;

// Generate .proto content for a single type (includes all dependencies)
string proto = ProtoSchemaGenerator.Generate(typeof(WeatherRequest));

// Generate for all [ProtoContract] and [ProtoService] types in an assembly
var allProto = ProtoSchemaGenerator.GenerateAll(assembly);

// Generate to disk (one .proto file per type/namespace/service)
List<string> paths = ProtoSchemaGenerator.GenerateToDirectory(assembly, "protos/");
```

### CLI Tool

```bash
dotnet run --project tools/ProtobuffEncoder.Tool -- \
  "src/ProtobuffEncoder.Contracts/bin/Debug/net10.0/ProtobuffEncoder.Contracts.dll" \
  "src/ProtobuffEncoder.Contracts/protos" \
  "src/ProtobuffEncoder.Contracts/ProtobuffEncoder.Contracts.csproj"
```

Arguments:

| Argument | Required | Description |
|----------|----------|-------------|
| `assembly-path` | Yes | Path to the compiled DLL containing `[ProtoContract]` types |
| `proto-output-dir` | Yes | Directory to write `.proto` files |
| `csproj-path` | No | `.csproj` file to auto-append `<Content>` entries for the generated proto files |
| `--verbose` | No | Show import relationships and service details |
| `--help` | No | Show usage information |

### MSBuild Integration

Import the targets file in your `.csproj` to auto-generate on build:

```xml
<Import Project="..\..\src\ProtobuffEncoder\build\ProtobuffEncoder.targets" />
```

Configure with properties:

```xml
<PropertyGroup>
  <ProtoOutputDir>protos</ProtoOutputDir>
  <GenerateProtoOnBuild>true</GenerateProtoOnBuild>
</PropertyGroup>
```

## File Grouping & Naming

The generator groups types into `.proto` files based on these rules:

| Rule | Condition | Example output |
|------|-----------|----------------|
| Named contract | `[ProtoContract(Name = "Order")]` | `Order.proto` |
| Versioned contract | `[ProtoContract(Version = 1)]` | `v1/<namespace>.proto` |
| Named + versioned | `[ProtoContract(Version = 1, Name = "Order")]` | `v1/Order.proto` |
| Service interface | `[ProtoService("OrderService")]` | `OrderService.proto` |
| Versioned service | `[ProtoService("OrderService", Version = 2)]` | `v2/OrderService.proto` |
| Default | No name/version | `<namespace>.proto` |

## Cross-File Imports {id="imports"}

When a type references another type that belongs to a different `.proto` file, the generator automatically adds an `import` statement.

### How it works

1. **Phase 1** — All `[ProtoContract]` and `[ProtoService]` types are scanned and assigned to file groups
2. **Phase 2** — Types are collected into their assigned file. Types belonging to a different file are skipped (not duplicated)
3. **Phase 3** — The import resolver walks all messages and services, checks which type names reference definitions in other files, and adds `import` statements
4. **Phase 4** — All files are rendered with their imports

### Example

Given these C# types:

```C#
[ProtoContract(Version = 1, Name = "Order")]
public class Order
{
    [ProtoField(1)] public Guid Id { get; set; }
    [ProtoField(2)] public CustomerDetails Customer { get; set; } = new();
    [ProtoField(3)] public List<OrderLineItem> Items { get; set; } = [];
}

[ProtoContract]
public class CustomerDetails
{
    [ProtoField(1)] public string FirstName { get; set; } = "";
    [ProtoField(2)] public ShippingAddress Address { get; set; } = new();
}

[ProtoService("OrderProcessingService")]
public interface IOrderProcessingService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<Order> GetOrderAsync(GetOrderRequest request);
}
```

The generator produces:

**`v1/Order.proto`** — imports the namespace file for `CustomerDetails`:
```protobuf
syntax = "proto3";
package MyApp.Models;

import "myapp_models.proto";

message Order {
  bytes Id = 1;
  CustomerDetails Customer = 2;
  repeated OrderLineItem Items = 3;
}
```

**`OrderProcessingService.proto`** — imports both message files:
```protobuf
syntax = "proto3";
package MyApp.Services;

import "GetOrderRequest.proto";
import "v1/Order.proto";

message GetOrderAsyncResponse {
  Order data = 1;
}

service OrderProcessingService {
  rpc GetOrderAsync (GetOrderRequest) returns (GetOrderAsyncResponse);
}
```

> No type definitions are duplicated across files — each type is defined exactly once in its canonical file.

## Service Generation {id="services"}

Types marked with `[ProtoService]` automatically generate gRPC service definitions with proper request/response message wiring.

### Auto-wrapping

When an RPC method's parameter or return type does not end with `Request` or `Response`, the generator creates a wrapper message:

| Method signature | Generated wrapper |
|-----------------|-------------------|
| `Task<Order> GetOrderAsync(GetOrderRequest req)` | `GetOrderRequest` used directly, `GetOrderAsyncResponse` wraps `Order` |
| `Task ExecuteAsync(Order order)` | `ExecuteAsyncRequest` wraps `Order`, `ExecuteAsyncResponse` is empty |
| `IAsyncEnumerable<Order> Stream(Empty req)` | `StreamRequest` wraps `Empty`, `StreamResponse` wraps `Order` |

### Streaming methods

The `[ProtoMethod]` attribute's `MethodType` maps to gRPC streaming keywords:

| ProtoMethodType | Proto syntax |
|----------------|-------------|
| `Unary` | `rpc Name (Req) returns (Res);` |
| `ServerStreaming` | `rpc Name (Req) returns (stream Res);` |
| `ClientStreaming` | `rpc Name (stream Req) returns (Res);` |
| `DuplexStreaming` | `rpc Name (stream Req) returns (stream Res);` |

### Service metadata

Add documentation comments to the generated `.proto` file:

```C#
[ProtoService("OrderService", Metadata = "Handles order lifecycle")]
public interface IOrderService { ... }
```

Produces:
```protobuf
// Metadata: Handles order lifecycle
service OrderService {
  ...
}
```

## Generated Output {id="output"}

### Simple message

```C#
[ProtoContract]
public class WeatherRequest
{
    public string City { get; set; } = "";
    public int Days { get; set; }
    public bool IncludeHourly { get; set; }
}
```

Produces:

```protobuf
syntax = "proto3";
package ProtobuffEncoder.Contracts;

message WeatherRequest {
  string City = 1;
  int32 Days = 2;
  bool IncludeHourly = 3;
}
```

### Supported constructs

The generator handles:

| Construct | Attribute/pattern |
|-----------|------------------|
| Nested messages | Property with `[ProtoContract]` type |
| Enums | Enum types (with or without `[ProtoContract]`) |
| Repeated fields | `List<T>`, `T[]`, `ICollection<T>` |
| Optional fields | `Nullable<T>` types |
| Map fields | `Dictionary<K,V>` with `[ProtoMap]` |
| OneOf groups | `[ProtoOneOf("group")]` |
| Deprecated | `[ProtoField(IsDeprecated = true)]` |
| Inheritance | `[ProtoInclude]` derived types |
| Implicit types | `[ProtoContract(ImplicitFields = true)]` |
| Services | `[ProtoService]` / `[ProtoMethod]` |
| Cross-file imports | Automatic when types span files |

## Parsing .proto Files

```C#
using ProtobuffEncoder.Schema;

// Parse a single .proto file
ProtoFile file = ProtoSchemaParser.ParseFile("protos/contracts.proto");

// Parse raw .proto content
ProtoFile file = ProtoSchemaParser.Parse(protoString);

// Parse all .proto files in a directory
List<ProtoFile> files = ProtoSchemaParser.ParseDirectory("protos/");
```

The parser supports: `message`, `enum`, `repeated`, `optional`, `oneof`, `map`, and `package` declarations.

## Schema-Based Decoding

A receiver with **no reference to your C# contract types** can decode messages using only `.proto` schemas.

### SchemaDecoder

```C#
using ProtobuffEncoder.Schema;

// Load from a directory of .proto files
var decoder = SchemaDecoder.FromDirectory("protos/");

// Or from a single file or raw content
var decoder = SchemaDecoder.FromFile("protos/contracts.proto");
var decoder = SchemaDecoder.FromProtoContent(protoString);

// List what's registered
IReadOnlyCollection<string> messages = decoder.RegisteredMessages;
IReadOnlyCollection<string> enums = decoder.RegisteredEnums;

// Get definitions
ProtoMessageDef? msg = decoder.GetMessage("WeatherRequest");
ProtoEnumDef? enumDef = decoder.GetEnum("NotificationLevel");
```

### DecodedMessage

The result of schema-based decoding is a `DecodedMessage` — a dictionary-like type with typed accessors.

```C#
DecodedMessage msg = decoder.Decode("WeatherRequest", bytes);

// Typed access
string city = msg.Get<string>("City");
long days = msg.Get<long>("Days");
bool hourly = msg.Get<bool>("IncludeHourly");

// Indexer (returns object?)
object? value = msg["City"];

// Repeated fields
List<string> tags = msg.GetRepeated<string>("Tags");

// Nested messages
DecodedMessage address = msg.GetMessage("HomeAddress");
List<DecodedMessage> forecasts = msg.GetMessages("Forecasts");
```

The decoder handles:
- All scalar types (varint, fixed32, fixed64, length-delimited)
- Packed repeated fields
- Nested messages (recursive)
- Enum name resolution
- Map fields
- Unknown fields (skipped gracefully)

## ProtobufWriter

Build protobuf messages by field number — no C# types needed.

```C#
using ProtobuffEncoder.Schema;

var writer = new ProtobufWriter();
writer.WriteString(1, "Amsterdam");
writer.WriteVarint(2, 3);
writer.WriteBool(3, true);
writer.WriteDouble(4, 52.3676);
writer.WriteFloat(5, 4.89f);
writer.WriteFixed64(6, timestamp);
writer.WriteBytes(7, rawData);

// Nested messages
var inner = new ProtobufWriter();
inner.WriteString(1, "2026-03-12");
inner.WriteDouble(2, 5.3);

var outer = new ProtobufWriter();
outer.WriteMessage(1, inner);
outer.WriteRepeatedMessage(2, [inner1, inner2]);
outer.WriteRepeatedString(3, ["a", "b", "c"]);
outer.WritePackedVarints(4, [1L, 2L, 3L]);

byte[] result = outer.ToByteArray();
```

## End-to-End: Receiver Without Contract References

The demo Receiver API has **zero compile-time dependency** on the Contracts project. It copies `.proto` files at build time and decodes purely from schemas.

### Project setup

```xml
<!-- Demo Receiver .csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\src\ProtobuffEncoder.AspNetCore\ProtobuffEncoder.AspNetCore.csproj" />
  <!-- NO reference to Contracts -->
</ItemGroup>

<!-- Copy .proto schemas from Contracts as the source of truth -->
<ItemGroup>
  <Content Include="..\..\src\ProtobuffEncoder.Contracts\protos\**\*.proto"
           Link="protos\%(Filename)%(Extension)">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### Runtime decoding

```C#
// Startup: load schemas
var protoDir = Path.Combine(AppContext.BaseDirectory, "protos");
builder.Services.AddSingleton(_ => SchemaDecoder.FromDirectory(protoDir));

// Endpoint: decode and respond
app.MapPost("/api/weather", async (HttpContext ctx, SchemaDecoder schema) =>
{
    byte[] bytes = await ReadBodyAsync(ctx);
    DecodedMessage request = schema.Decode("WeatherRequest", bytes);

    string city = request.Get<string>("City") ?? "Unknown";
    int days = request["Days"] is long d ? (int)d : 3;

    // Build response with ProtobufWriter
    var response = new ProtobufWriter();
    response.WriteString(1, city);
    response.WriteRepeatedMessage(2, forecasts);
    response.WriteFixed64(3, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    return Results.Bytes(response.ToByteArray(), "application/x-protobuf");
});
```
