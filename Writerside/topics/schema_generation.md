# Schema Generation

The `ProtoSchemaGenerator` generates `.proto` schema files from C# types marked with `[ProtoContract]` and `[ProtoService]`. It supports all protobuf features: messages, enums, nested types, map fields, oneof groups, services with all four RPC method types, cross-file imports, and versioned directory output.

## Single Type Generation

Generate a `.proto` file for a single type and all its dependencies:

```C#
using ProtobuffEncoder.Schema;

string proto = ProtoSchemaGenerator.Generate(typeof(OrderMessage));
```

Output:

```
syntax = "proto3";

package MyApp.Models;

message OrderMessage {
  int32 OrderId = 1;
  string CustomerName = 2;
  double Total = 3;
  repeated OrderItem Items = 4;
}

message OrderItem {
  int32 ProductId = 1;
  string ProductName = 2;
  int32 Quantity = 3;
  double Price = 4;
}
```

## Assembly-Wide Generation

Generate all `.proto` files for every `[ProtoContract]` and `[ProtoService]` type in an assembly:

```C#
Dictionary<string, string> files = ProtoSchemaGenerator.GenerateAll(assembly);

foreach (var (filename, content) in files)
    Console.WriteLine($"--- {filename} ---\n{content}");
```

## Generate to Directory

Write all generated `.proto` files to disk:

```C#
List<string> paths = ProtoSchemaGenerator.GenerateToDirectory(assembly, "./proto-output");
// Returns: ["./proto-output/myapp_models.proto", "./proto-output/v1/Order.proto", ...]
```

## File Naming and Grouping

Types are grouped into `.proto` files based on:

| Scenario | File Key |
|----------|----------|
| No `Name` or `Version` | `{namespace}.proto` (dots replaced with underscores, lowercase) |
| `[ProtoContract(Name = "Order")]` | `Order.proto` |
| `[ProtoContract(Version = 1)]` | `v1/{namespace}.proto` |
| `[ProtoContract(Version = 1, Name = "Order")]` | `v1/Order.proto` |
| `[ProtoService("OrderService")]` | `OrderService.proto` |
| `[ProtoService("OrderService", Version = 2)]` | `v2/OrderService.proto` |

## Cross-File Imports

When a type references another type that belongs to a different file, import statements are automatically generated:

```C#
[ProtoContract(Version = 1, Name = "Order")]
public class Order
{
    [ProtoField(1)] public string OrderId { get; set; } = "";
    [ProtoField(2)] public CustomerDetails Customer { get; set; } = new();
}

[ProtoContract(Name = "CustomerDetails")]
public class CustomerDetails
{
    [ProtoField(1)] public string Name { get; set; } = "";
}
```

Generated `v1/Order.proto`:

```
syntax = "proto3";

import "CustomerDetails.proto";

message Order {
  string OrderId = 1;
  CustomerDetails Customer = 2;
}
```

The import resolver:

1. Builds a type-to-file-key registry for all types in the assembly
2. Scans message fields, map types, and service RPC types for cross-file references
3. Adds `import` statements for any referenced type in another file
4. Skips scalar proto types (`string`, `int32`, etc.)

## Service Generation

Interfaces decorated with `[ProtoService]` generate gRPC service definitions:

```C#
[ProtoService("OrderProcessingService")]
public interface IOrderProcessingService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<Order> GetOrderAsync(GetOrderRequest request);

    [ProtoMethod(ProtoMethodType.DuplexStreaming)]
    IAsyncEnumerable<Order> ProcessOrdersAsync(
        IAsyncEnumerable<Order> orders, CancellationToken ct);
}
```

Generated:

```
service OrderProcessingService {
  rpc GetOrderAsync (GetOrderRequest) returns (Order);
  rpc ProcessOrdersAsync (stream Order) returns (stream Order);
}
```

### Request/Response Wrapping

If a method's parameter or return type doesn't end in `Request` or `Response`, the generator auto-creates wrapper messages:

```C#
[ProtoMethod(ProtoMethodType.Unary)]
Task<Order> GetOrder(string orderId); // orderId is not a *Request type
```

Generates:

```
message GetOrderRequest {
  string data = 1;
}
message GetOrderResponse {
  Order data = 1;
}
service MyService {
  rpc GetOrder (GetOrderRequest) returns (GetOrderResponse);
}
```

## Supported Schema Features

| Feature | Generated Proto Syntax |
|---------|----------------------|
| Scalar fields | `int32 Name = N;` |
| Repeated fields | `repeated Type Name = N;` |
| Optional fields | `optional Type Name = N;` |
| Map fields | `map<KeyType, ValueType> Name = N;` |
| OneOf groups | `oneof GroupName { ... }` |
| Nested messages | `message Inner { ... }` inside parent |
| Enums | `enum Name { VALUE = 0; ... }` |
| Deprecated | `Type Name = N [deprecated = true];` |
| ProtoInclude | `optional DerivedType Name = N;` |
| Services | `service Name { rpc ... }` |
| Imports | `import "other.proto";` |
| Metadata comments | `// Metadata: ...` |
| Source tracing | `// Imported from C# class: Namespace.Type` |

## ProtoFile Model

The in-memory model used during generation:

```
ProtoFile
├── Syntax ("proto3")
├── Package (namespace)
├── FilePath ("v1/Order.proto")
├── Imports ["other.proto", ...]
├── Messages []
│   └── ProtoMessageDef
│       ├── Name, SourceType, Metadata
│       ├── Fields [] (ProtoFieldDef)
│       ├── NestedMessages []
│       ├── NestedEnums []
│       └── OneOfs [] (ProtoOneOfDef)
├── Enums []
│   └── ProtoEnumDef { Name, Values [] }
└── Services []
    └── ProtoServiceDef
        ├── Name, SourceType, Metadata
        └── Methods [] (ProtoRpcDef)
```

