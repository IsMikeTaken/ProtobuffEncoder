# Attributes

ProtobuffEncoder uses C# attributes to control how classes and properties are serialized to protobuf binary format.

## `[ProtoContract]`

Applied to a class or struct to opt it into protobuf serialization. Without this attribute (or `ImplicitFields` on a parent), a class will not be serialized.

```csharp
[ProtoContract]
public class Sensor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
```

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ExplicitFields` | `bool` | `false` | When `true`, only properties marked with `[ProtoField]` are included. When `false`, all public properties are auto-included. |
| `IncludeBaseFields` | `bool` | `false` | When `true`, base class properties are included in serialization (walked up the inheritance chain). |
| `ImplicitFields` | `bool` | `false` | When `true`, nested object properties whose types lack `[ProtoContract]` are implicitly treated as contracts and auto-serialized. |
| `SkipDefaults` | `bool` | `true` | When `true`, fields holding their type's default value are skipped (proto3 behavior). Individual fields can override via `ProtoField.WriteDefault`. |
| `Version` | `int` | `0` | Specifies the version of the contract for schema generation (e.g., outputs to `v1/` directory). |
| `Metadata` | `string?` | `null` | Optional comment or metadata to include in the generated `.proto` file. |

### Constructors

Convenience constructors for quick naming and versioning:

- `[ProtoContract("MyName")]`: Sets the `Name` property.
- `[ProtoContract(2)]`: Sets the `Version` property.

### Enum Support

`[ProtoContract]` can also be applied to enums to provide versioning or metadata for schema generation.

```csharp
[ProtoContract(Version = 1, Metadata = "Internal priority levels")]
public enum Priority { Low, Medium, High }
```

### Explicit fields mode

```csharp
[ProtoContract(ExplicitFields = true)]
public class Sensor
{
    [ProtoField(FieldNumber = 1)]
    public int Id { get; set; }

    // Not serialized — no [ProtoField] and ExplicitFields is true
    public string DebugInfo { get; set; } = "";
}
```

### Base class fields

```csharp
[ProtoContract(IncludeBaseFields = true)]
public class AdminUser : User
{
    public string Department { get; set; } = "";
}
```

### Implicit nested serialization

```csharp
[ProtoContract(ImplicitFields = true)]
public class Order
{
    public string Id { get; set; } = "";

    // Serialized even though ShippingInfo has no [ProtoContract]
    public ShippingInfo Shipping { get; set; } = new();
}
```

---

## `[ProtoField]`

Applied to a property to override its protobuf metadata.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FieldNumber` | `int` | auto-assigned | The protobuf field number (1-based). When 0, auto-assigned. |
| `Name` | `string?` | property name | Override the field name used in the `.proto` schema. |
| `WireType` | `WireType?` | inferred | Force a specific wire type. |
| `WriteDefault` | `bool` | `false` | Write the field even when it holds the default value. |
| `IsPacked` | `bool?` | `null` (auto) | Control packed encoding for repeated scalar fields. `null` = auto-detect (proto3 default: packed). |
| `IsDeprecated` | `bool` | `false` | Marks the field as deprecated in generated `.proto` schemas. Still serialized. |
| `IsRequired` | `bool` | `false` | Encoding throws if the value is null or default. Library-level enforcement. |

### Constructors

- `[ProtoField(1)]`: Shorthand for `[ProtoField(FieldNumber = 1)]`.
- `[ProtoField]`: Default constructor for auto-assigned field numbers.

```csharp
[ProtoContract]
public class Event
{
    [ProtoField(FieldNumber = 10, Name = "event_name", WriteDefault = true)]
    public string Name { get; set; } = "";

    [ProtoField(IsRequired = true)]
    public string Source { get; set; } = "";

    [ProtoField(IsDeprecated = true)]
    public string LegacyId { get; set; } = "";
}
```

---

## `[ProtoIgnore]`

Excludes a property from serialization entirely.

```csharp
[ProtoContract]
public class User
{
    public string Name { get; set; } = "";

    [ProtoIgnore]
    public string PasswordHash { get; set; } = "";
}
```

---

## `[ProtoMap]`

Marks a `Dictionary<TKey, TValue>` property as a protobuf map field. Without this attribute, dictionaries are not serialized.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `KeyType` | `string?` | inferred | Override the key's proto type (e.g. `"string"`, `"int32"`). |
| `ValueType` | `string?` | inferred | Override the value's proto type. |

```csharp
[ProtoContract]
public class UserProfile
{
    [ProtoMap]
    public Dictionary<string, string> Preferences { get; set; } = new();

    [ProtoMap]
    public Dictionary<string, Address> Addresses { get; set; } = new();
}
```

Generated `.proto` output:

```proto
message UserProfile {
  map<string, string> Preferences = 1;
  map<string, Address> Addresses = 2;
}
```

---

## `[ProtoOneOf]`

Groups properties into a protobuf `oneof` union. At most one property in a group should have a non-default value. During encoding, only the first non-default property in the group is written.

```csharp
[ProtoContract]
public class Contact
{
    [ProtoOneOf("primary_contact")]
    public string? Email { get; set; }

    [ProtoOneOf("primary_contact")]
    public string? Phone { get; set; }
}
```

Generated `.proto` output:

```proto
message Contact {
  oneof primary_contact {
    string Email = 1;
    string Phone = 2;
  }
}
```

---

## `[ProtoInclude]`

Declares known derived types on a base class for polymorphic serialization. Each derived type is encoded as a nested message at the specified field number.

| Parameter | Type | Description |
|-----------|------|-------------|
| `fieldNumber` | `int` | Field number for the derived type's nested message. Must be unique and not collide with base fields. |
| `derivedType` | `Type` | The derived CLR type. |

```csharp
[ProtoContract]
[ProtoInclude(10, typeof(Dog))]
[ProtoInclude(11, typeof(Cat))]
public class Animal
{
    public string Name { get; set; } = "";
}

[ProtoContract(IncludeBaseFields = true)]
public class Dog : Animal
{
    public string Breed { get; set; } = "";
}

[ProtoContract(IncludeBaseFields = true)]
public class Cat : Animal
{
    public bool IsIndoor { get; set; }
}
```

---

## Full example combining all attributes

```csharp
[ProtoContract(IncludeBaseFields = true)]
[ProtoInclude(20, typeof(AdminProfile))]
public class UserProfile
{
    public string DisplayName { get; set; } = "";

    [ProtoField(IsRequired = true)]
    public string Email { get; set; } = "";

    public int Age { get; set; }

    public Address? PrimaryAddress { get; set; }

    [ProtoMap]
    public Dictionary<string, string> Preferences { get; set; } = new();

    [ProtoOneOf("primary_contact")]
    public string? PhoneNumber { get; set; }

    [ProtoOneOf("primary_contact")]
    public string? SlackHandle { get; set; }

    [ProtoField(IsDeprecated = true)]
    public string LegacyId { get; set; } = "";

    [ProtoIgnore]
    public string InternalNote { get; set; } = "";
}

[ProtoContract(IncludeBaseFields = true)]
public class AdminProfile : UserProfile
{
    public string Department { get; set; } = "";
}
```
