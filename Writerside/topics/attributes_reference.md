# Attributes Reference

ProtobuffEncoder uses .NET attributes to define protobuf contracts without `.proto` files. All attributes are in the `ProtobuffEncoder.Attributes` namespace.

## ProtoContract

Marks a class, struct, or enum for protobuf serialization.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum)]
public sealed class ProtoContractAttribute : Attribute
```

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ExplicitFields` | `bool` | `false` | When `true`, only `[ProtoField]`-decorated properties are serialized |
| `IncludeBaseFields` | `bool` | `false` | When `true`, base class properties are included in serialization |
| `ImplicitFields` | `bool` | `false` | When `true`, nested types without `[ProtoContract]` are auto-serialized |
| `SkipDefaults` | `bool` | `true` | When `true`, default-valued fields are omitted (proto3 behavior) |
| `Version` | `int` | `0` | API version; generates to `v{Version}/` subdirectory in schema output |
| `Name` | `string?` | `null` | Override the protobuf message name and file name |
| `Metadata` | `string?` | `null` | Comment added to the generated `.proto` definition |

### Examples

```csharp
// Basic contract - all public properties included automatically
[ProtoContract]
public class SimpleMessage
{
    [ProtoField(1)] public int Id { get; set; }
    [ProtoField(2)] public string Name { get; set; } = "";
}

// Explicit fields only
[ProtoContract(ExplicitFields = true)]
public class StrictMessage
{
    [ProtoField(1)] public int Id { get; set; }
    public string NotSerialized { get; set; } = ""; // excluded
}

// Versioned with custom name
[ProtoContract(Version = 1, Name = "Order")]
public class OrderV1
{
    [ProtoField(1)] public string OrderId { get; set; } = "";
}

// Implicit nested types
[ProtoContract(ImplicitFields = true)]
public class AutoMessage
{
    [ProtoField(1)] public string Name { get; set; } = "";
    [ProtoField(2)] public Address Address { get; set; } = new(); // no [ProtoContract] needed
}

// Include base class fields
[ProtoContract(IncludeBaseFields = true)]
public class DerivedMessage : BaseMessage
{
    [ProtoField(3)] public string Extra { get; set; } = "";
}
```

## ProtoField

Overrides protobuf field metadata for a property.

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class ProtoFieldAttribute : Attribute
```

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FieldNumber` | `int` | `0` | 1-based field number. `0` = auto-assigned by declaration order |
| `Name` | `string?` | `null` | Override field name in generated schema |
| `WireType` | `WireType?` | `null` | Force specific wire type (normally inferred from CLR type) |
| `WriteDefault` | `bool` | `false` | Write field even when value is the type's default |
| `IsPacked` | `bool?` | `null` | Control packed encoding for repeated scalar fields |
| `IsDeprecated` | `bool` | `false` | Marks field as `[deprecated = true]` in schema |
| `IsRequired` | `bool` | `false` | Throws if field is null/default during encoding |

### Examples

```csharp
[ProtoContract]
public class DetailedMessage
{
    [ProtoField(1, IsRequired = true)]
    public int Id { get; set; }

    [ProtoField(2, Name = "display_name")]
    public string DisplayName { get; set; } = "";

    [ProtoField(3, WriteDefault = true)]
    public int Count { get; set; } // written even when 0

    [ProtoField(4, IsDeprecated = true)]
    public string? LegacyField { get; set; }

    [ProtoField(5, IsPacked = false)]
    public List<int> UnpackedNumbers { get; set; } = [];
}
```

## ProtoMap

Marks a `Dictionary<K,V>` property as a protobuf map field.

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class ProtoMapAttribute : Attribute
```

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `KeyType` | `string?` | `null` | Override key's proto type name |
| `ValueType` | `string?` | `null` | Override value's proto type name |

### Example

```csharp
[ProtoContract]
public class ConfigMessage
{
    [ProtoMap]
    [ProtoField(1)]
    public Dictionary<string, string> Settings { get; set; } = new();

    [ProtoMap]
    [ProtoField(2)]
    public Dictionary<int, string> Labels { get; set; } = new();
}
```

Generated schema:

```protobuf
message ConfigMessage {
  map<string, string> Settings = 1;
  map<int32, string> Labels = 2;
}
```

## ProtoOneOf

Groups properties into a protobuf `oneof` union. At most one property in the group should have a non-default value.

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class ProtoOneOfAttribute : Attribute
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `GroupName` | `string` | Name of the oneof group |

### Example

```csharp
[ProtoContract]
public class ContactInfo
{
    [ProtoField(1)] public string Name { get; set; } = "";

    [ProtoOneOf("contact")]
    [ProtoField(2)] public string? Email { get; set; }

    [ProtoOneOf("contact")]
    [ProtoField(3)] public string? Phone { get; set; }

    [ProtoOneOf("contact")]
    [ProtoField(4)] public string? Address { get; set; }
}
```

Generated schema:

```protobuf
message ContactInfo {
  string Name = 1;
  oneof contact {
    string Email = 2;
    string Phone = 3;
    string Address = 4;
  }
}
```

During encoding, only the **first non-default** property in the group is written.

## ProtoInclude

Declares a known derived type for polymorphic serialization.

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ProtoIncludeAttribute : Attribute
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `FieldNumber` | `int` | Field number for the derived type's nested message |
| `DerivedType` | `Type` | The derived CLR type |

### Example

```csharp
[ProtoContract]
[ProtoInclude(10, typeof(Dog))]
[ProtoInclude(11, typeof(Cat))]
public class Animal
{
    [ProtoField(1)] public string Name { get; set; } = "";
}

[ProtoContract(IncludeBaseFields = true)]
public class Dog : Animal
{
    [ProtoField(2)] public string Breed { get; set; } = "";
}

[ProtoContract(IncludeBaseFields = true)]
public class Cat : Animal
{
    [ProtoField(2)] public bool IsIndoor { get; set; }
}
```

The derived type's fields are encoded as a nested message at the specified field number.

## ProtoIgnore

Excludes a property from serialization.

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class ProtoIgnoreAttribute : Attribute
```

### Example

```csharp
[ProtoContract]
public class UserProfile
{
    [ProtoField(1)] public int Id { get; set; }
    [ProtoField(2)] public string Name { get; set; } = "";
    [ProtoIgnore] public string PasswordHash { get; set; } = ""; // never serialized
}
```

## ProtoService

Marks an interface as a gRPC service definition for code-first service generation.

```csharp
[AttributeUsage(AttributeTargets.Interface)]
public sealed class ProtoServiceAttribute : Attribute
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `ServiceName` | `string` | Name of the gRPC service |
| `Version` | `int` | API version for schema output directory |
| `Metadata` | `string?` | Comment for generated `.proto` |

## ProtoMethod

Marks a method on a `[ProtoService]` interface as an RPC method.

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class ProtoMethodAttribute : Attribute
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `MethodType` | `ProtoMethodType` | `Unary`, `ServerStreaming`, `ClientStreaming`, or `DuplexStreaming` |
| `Name` | `string?` | Override RPC method name |

### Example

```csharp
[ProtoService("WeatherService")]
public interface IWeatherService
{
    [ProtoMethod(ProtoMethodType.Unary)]
    Task<WeatherResponse> GetForecast(WeatherRequest request);

    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    IAsyncEnumerable<WeatherUpdate> StreamUpdates(WeatherRequest request, CancellationToken ct);

    [ProtoMethod(ProtoMethodType.ClientStreaming)]
    Task<WeatherSummary> UploadReadings(IAsyncEnumerable<SensorReading> readings, CancellationToken ct);

    [ProtoMethod(ProtoMethodType.DuplexStreaming)]
    IAsyncEnumerable<Alert> Monitor(IAsyncEnumerable<SensorReading> readings, CancellationToken ct);
}
```

## Wire Types

The `WireType` enum controls how values are encoded on the wire:

| WireType | Value | Used For |
|----------|-------|----------|
| `Varint` | 0 | `int`, `uint`, `long`, `ulong`, `bool`, `enum`, `byte`, `short` |
| `Fixed64` | 1 | `double`, `long`, `ulong`, `DateTime`, `TimeSpan` |
| `LengthDelimited` | 2 | `string`, `byte[]`, nested messages, `decimal`, `Guid`, `BigInteger` |
| `Fixed32` | 5 | `float`, `int` (when forced), `uint` |
