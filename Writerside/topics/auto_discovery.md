# Auto-Discovery and Field Numbering

ProtobuffEncoder supports automatic type discovery and configurable field numbering strategies, allowing you to serialize plain C# classes without requiring `[ProtoContract]` attributes on every type.

## Overview

By default, every type must be marked with `[ProtoContract]` to be serializable. Auto-discovery removes this requirement through two mechanisms:

- **Explicit registration** via `ProtoRegistry.Register<T>()` — opt-in per type
- **Global auto-discover** via `ProtoRegistry.Configure()` — opt-in for all types

Field numbers are then assigned automatically using one of three strategies.

## ProtoRegistry

The `ProtoRegistry` class is the central configuration point for auto-discovery.

### Configuration

```C#
// Enable auto-discovery globally
ProtoRegistry.Configure(opts =>
{
    opts.AutoDiscover = true;
    opts.DefaultFieldNumbering = FieldNumbering.Alphabetical;
    opts.DefaultEncoding = "utf-8"; // optional
});
```

### Registering Individual Types

```C#
// Register a plain DTO for serialization
ProtoRegistry.Register<CustomerDto>();

// Register with a specific field numbering strategy
ProtoRegistry.Register<OrderDto>(FieldNumbering.Alphabetical);
```

### Assembly Scanning

```C#
// Register all public classes with read/write properties
int count = ProtoRegistry.RegisterAssembly(typeof(Program).Assembly);
Console.WriteLine($"Registered {count} types");
```

`RegisterAssembly` skips types that already have `[ProtoContract]` — they are handled by the standard resolver.

### Checking Registration

```C#
bool registered = ProtoRegistry.IsRegistered(typeof(CustomerDto));
bool resolvable = ProtoRegistry.IsResolvable(typeof(CustomerDto)); // registered OR [ProtoContract] OR auto-discover
```

## Field Numbering Strategies

Field numbers determine wire compatibility. ProtobuffEncoder provides three strategies for auto-assigning field numbers to properties that don't have an explicit `[ProtoField(N)]` number.

### Declaration Order (Default)

Fields are numbered sequentially in the order they appear in source code. This is the simplest strategy and matches standard protobuf convention.

```C#
public class UserDto
{
    public string Name { get; set; } = "";    // field 1
    public int Age { get; set; }              // field 2
    public string Email { get; set; } = "";   // field 3
}
```

> **Warning**: Reordering properties in source code changes field numbers, which breaks wire compatibility with previously serialized data.

### Alphabetical

Fields are numbered alphabetically by property name. This produces deterministic numbering regardless of source code ordering.

```C#
[ProtoContract(FieldNumbering = FieldNumbering.Alphabetical)]
public class UserDto
{
    public string Name { get; set; } = "";    // field 2 (alphabetically)
    public int Age { get; set; }              // field 1 (alphabetically)
    public string Email { get; set; } = "";   // field 3 (alphabetically)
}
```

> **Tip**: Alphabetical ordering is recommended when multiple developers may reorder properties independently. Adding new properties is safe as long as existing property names don't change.

### Type Then Alphabetical

Groups properties by type category, then sorts alphabetically within each group:
1. **Scalars** — `bool`, `int`, `string`, `double`, `enum`, etc.
2. **Collections** — `List<T>`, `T[]`, `Dictionary<K,V>`, etc.
3. **Messages** — nested contract types

```C#
[ProtoContract(FieldNumbering = FieldNumbering.TypeThenAlphabetical)]
public class OrderDto
{
    public OrderDetails Details { get; set; } = new();  // field 4 (message)
    public List<string> Tags { get; set; } = [];        // field 3 (collection)
    public string Name { get; set; } = "";              // field 2 (scalar)
    public int Id { get; set; }                         // field 1 (scalar)
}
```

## Priority Order

Field numbering is resolved with the following priority (highest first):

1. **Per-type registration** — `ProtoRegistry.Register<T>(FieldNumbering.Alphabetical)`
2. **Contract attribute** — `[ProtoContract(FieldNumbering = FieldNumbering.Alphabetical)]`
3. **Global default** — `ProtoRegistry.Options.DefaultFieldNumbering`
4. **Built-in default** — `FieldNumbering.DeclarationOrder`

## Explicit Field Numbers

Properties with explicit `[ProtoField(N)]` field numbers are **never reordered** by any strategy. The numbering strategy only affects auto-assigned properties. Auto-assignment skips any numbers already used by explicit fields.

```C#
[ProtoContract(FieldNumbering = FieldNumbering.Alphabetical)]
public class MixedMessage
{
    [ProtoField(10)] public int FixedId { get; set; }   // always field 10
    public string Zebra { get; set; } = "";              // auto: field 2 (skips 10)
    public string Alpha { get; set; } = "";              // auto: field 1
}
```

## End-to-End Example

```C#
// 1. Configure global auto-discovery
ProtoRegistry.Configure(opts =>
{
    opts.AutoDiscover = true;
    opts.DefaultFieldNumbering = FieldNumbering.Alphabetical;
});

// 2. Define a plain DTO (no [ProtoContract] needed)
public class SensorReading
{
    public double Temperature { get; set; }  // field 2 (alphabetical)
    public string Location { get; set; }     // field 1 (alphabetical)
}

// 3. Encode and decode
var reading = new SensorReading { Temperature = 23.5, Location = "Lab-A" };
byte[] bytes = ProtobufEncoder.Encode(reading);
var decoded = ProtobufEncoder.Decode<SensorReading>(bytes);
// decoded.Temperature == 23.5, decoded.Location == "Lab-A"
```

## Reset (Testing)

For test isolation, call `ProtoRegistry.Reset()` to clear all registrations and restore default options:

```C#
[Fact]
public void MyTest()
{
    ProtoRegistry.Reset();
    ProtoRegistry.Register<MyDto>();
    // ... test logic ...
    ProtoRegistry.Reset(); // cleanup
}
```
