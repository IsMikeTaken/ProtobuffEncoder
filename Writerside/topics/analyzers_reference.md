# Roslyn Analyzers Reference

ProtobuffEncoder ships a Roslyn analyzer package (`ProtobuffEncoder.Analyzers`) that catches common
serialisation mistakes at compile time. Install it as a project reference or NuGet package and the
diagnostics appear as warnings or errors in your IDE and CI builds.

## Diagnostics

### PROTO001 — ProtoContract has no serializable fields

| | |
|---|---|
| **Severity** | Warning |
| **Analyzer** | `ProtoContractAnalyzer` |

A type marked with `[ProtoContract]` has no public properties with both a getter and a setter.
Nothing will be serialised.

```C#
[ProtoContract]
public class Empty { } // PROTO001
```

**Fix:** add at least one read/write property, or remove `[ProtoContract]`.

---

### PROTO002 — Duplicate protobuf field number

| | |
|---|---|
| **Severity** | Error |
| **Analyzer** | `ProtoContractAnalyzer` |

Two or more `[ProtoField]` attributes share the same field number on one type. This causes data
corruption during serialisation.

```C#
[ProtoContract]
public class Dup
{
    [ProtoField(1)] public string A { get; set; }
    [ProtoField(1)] public string B { get; set; } // PROTO002
}
```

**Fix:** give each field a unique number.

---

### PROTO003 — ProtoContract type has no parameterless constructor

| | |
|---|---|
| **Severity** | Error |
| **Analyzer** | `ProtoContractAnalyzer` |

The protobuf decoder creates instances via `Activator.CreateInstance`, which requires a parameterless
constructor. Deserialisation will fail at runtime without one.

```C#
[ProtoContract]
public class NoCtor // PROTO003
{
    public NoCtor(int x) { }
    [ProtoField(1)] public int X { get; set; }
}
```

**Fix:** add a parameterless constructor (it can be `internal` or `private`).

---

### PROTO004 — Serialised property has no setter

| | |
|---|---|
| **Severity** | Warning |
| **Analyzer** | `ProtoContractAnalyzer` |

A public property with a getter but no setter will be encoded but cannot be decoded. This usually
indicates a mistake.

```C#
[ProtoContract]
public class ReadOnlyProp
{
    public string Name { get; } // PROTO004
    [ProtoField(1)] public int Id { get; set; }
}
```

**Fix:** add a setter, or mark the property with `[ProtoIgnore]`.

---

### PROTO005 — ProtoField used without ProtoContract

| | |
|---|---|
| **Severity** | Warning |
| **Analyzer** | `ProtoFieldAnalyzer` |

A property has `[ProtoField]` but the containing type is not marked with `[ProtoContract]`. The
attribute has no effect unless the type is registered via `ProtoRegistry` or used as an implicit
nested type.

```C#
public class Orphan
{
    [ProtoField(1)] public string Name { get; set; } // PROTO005
}
```

**Fix:** add `[ProtoContract]` to the type, or register it via `ProtoRegistry.Register<T>()`.

---

### PROTO006 — Invalid protobuf field number

| | |
|---|---|
| **Severity** | Error |
| **Analyzer** | `ProtoFieldAnalyzer` |

Protobuf field numbers must be between 1 and 536,870,911 (2^29 - 1).

```C#
[ProtoContract]
public class Bad
{
    [ProtoField(-1)] public string Name { get; set; } // PROTO006
}
```

---

### PROTO007 — Reserved protobuf field number

| | |
|---|---|
| **Severity** | Warning |
| **Analyzer** | `ProtoFieldAnalyzer` |

Field numbers 19,000 through 19,999 are reserved by the protobuf wire format for internal use.

```C#
[ProtoContract]
public class Reserved
{
    [ProtoField(19000)] public string Name { get; set; } // PROTO007
}
```

---

### PROTO008 — Mutable struct as ProtoContract

| | |
|---|---|
| **Severity** | Info |
| **Analyzer** | `ProtoContractAnalyzer` |

Struct types are boxed during encoding/decoding, which may cause copies and unexpected mutation
behaviour. Consider using a class instead.

```C#
[ProtoContract]
public struct Point // PROTO008
{
    [ProtoField(1)] public int X { get; set; }
    [ProtoField(2)] public int Y { get; set; }
}
```

---

### PROTO009 — OneOf group has only one member

| | |
|---|---|
| **Severity** | Warning |
| **Analyzer** | `ProtoContractAnalyzer` |

A `[ProtoOneOf]` group with a single member provides no semantic benefit. A oneof should have at
least two alternatives.

```C#
[ProtoContract]
public class SingleOneOf
{
    [ProtoField(1)]
    [ProtoOneOf("channel")]
    public string Email { get; set; } // PROTO009 — only one member in "channel"
}
```

---

### PROTO010 — Unrecognised encoding name

| | |
|---|---|
| **Severity** | Warning |
| **Analyzer** | `ProtoFieldAnalyzer` |

The encoding name on `[ProtoField(Encoding = "...")]` or `[ProtoContract(DefaultEncoding = "...")]`
is not recognised. Valid values include `utf-8`, `utf-16`, `utf-32`, `ascii`, and `latin-1`.

```C#
[ProtoContract]
public class BadEnc
{
    [ProtoField(1, Encoding = "rot13")] public string Name { get; set; } // PROTO010
}
```

---

### PROTO011 — ProtoService has no methods

| | |
|---|---|
| **Severity** | Warning |
| **Analyzer** | `ProtoServiceAnalyzer` |

An interface marked with `[ProtoService]` has no methods decorated with `[ProtoMethod]`. A service
should declare at least one RPC operation.

```C#
[ProtoService("EmptyService")]
public interface IEmptyService { } // PROTO011
```

---

### PROTO012 — Streaming method has wrong return type

| | |
|---|---|
| **Severity** | Error |
| **Analyzer** | `ProtoServiceAnalyzer` |

A `[ProtoMethod]` marked as `ServerStreaming` or `DuplexStreaming` must return
`IAsyncEnumerable<T>`. Unary and ClientStreaming methods must return `Task<T>`.

```C#
[ProtoService("BadService")]
public interface IBadService
{
    [ProtoMethod(ProtoMethodType.ServerStreaming)]
    Task<string> Stream(string input); // PROTO012
}
```

---

### PROTO013 — ProtoInclude field number conflicts with ProtoField

| | |
|---|---|
| **Severity** | Error |
| **Analyzer** | `ProtoIncludeAnalyzer` |

A `[ProtoInclude]` field number collides with an existing `[ProtoField]` number on the same type.
They share the same namespace and a conflict causes data corruption.

```C#
[ProtoContract]
[ProtoInclude(1, typeof(Dog))] // PROTO013 — conflicts with Name's field number
public class Animal
{
    [ProtoField(1)] public string Name { get; set; }
}
```

---

### PROTO014 — ProtoInclude type is not a subclass

| | |
|---|---|
| **Severity** | Error |
| **Analyzer** | `ProtoIncludeAnalyzer` |

The type specified in `[ProtoInclude]` must inherit from the type that carries the attribute.
Otherwise polymorphic deserialisation will fail.

```C#
[ProtoContract]
[ProtoInclude(10, typeof(Unrelated))] // PROTO014
public class Base
{
    [ProtoField(1)] public string Name { get; set; }
}

public class Unrelated { }
```

---

### PROTO015 — ProtoMap on non-Dictionary property

| | |
|---|---|
| **Severity** | Error |
| **Analyzer** | `ProtoIncludeAnalyzer` |

The `[ProtoMap]` attribute is only valid on `Dictionary<TKey, TValue>` properties. Using it on
other types has no effect and indicates a mistake.

```C#
[ProtoContract]
public class BadMap
{
    [ProtoMap]
    [ProtoField(1)]
    public List<string> Items { get; set; } // PROTO015
}
```

## Summary table

| Rule | Category | Severity | Description |
|------|----------|----------|-------------|
| PROTO001 | ProtoContractAnalyzer | Warning | No serializable fields |
| PROTO002 | ProtoContractAnalyzer | Error | Duplicate field number |
| PROTO003 | ProtoContractAnalyzer | Error | Missing parameterless constructor |
| PROTO004 | ProtoContractAnalyzer | Warning | Property without setter |
| PROTO005 | ProtoFieldAnalyzer | Warning | ProtoField without ProtoContract |
| PROTO006 | ProtoFieldAnalyzer | Error | Invalid field number |
| PROTO007 | ProtoFieldAnalyzer | Warning | Reserved field number range |
| PROTO008 | ProtoContractAnalyzer | Info | Mutable struct contract |
| PROTO009 | ProtoContractAnalyzer | Warning | Single-member OneOf group |
| PROTO010 | ProtoFieldAnalyzer | Warning | Unrecognised encoding name |
| PROTO011 | ProtoServiceAnalyzer | Warning | Service with no methods |
| PROTO012 | ProtoServiceAnalyzer | Error | Streaming return type mismatch |
| PROTO013 | ProtoIncludeAnalyzer | Error | Include/field number conflict |
| PROTO014 | ProtoIncludeAnalyzer | Error | Include type not derived |
| PROTO015 | ProtoIncludeAnalyzer | Error | ProtoMap on non-Dictionary |
