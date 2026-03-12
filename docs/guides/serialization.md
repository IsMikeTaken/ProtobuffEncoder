# Serialization

## Encoding & Decoding

```csharp
using ProtobuffEncoder;

// Encode to byte[]
byte[] bytes = ProtobufEncoder.Encode(person);

// Decode from byte[]
var person = ProtobufEncoder.Decode<Person>(bytes);

// Encode to stream
ProtobufEncoder.Encode(person, stream);
await ProtobufEncoder.EncodeAsync(person, stream, cancellationToken);

// Decode from stream
var result = await ProtobufEncoder.DecodeAsync<Person>(stream, cancellationToken);
```

## Field Numbering

By default, all public properties (except `[ProtoIgnore]`) are assigned incrementing field numbers starting at 1, based on declaration order. Explicitly assigned numbers are reserved first, and auto-assignment skips them to prevent collisions.

```csharp
[ProtoContract]
public class Example
{
    public string A { get; set; } = "";          // auto -> field 1

    [ProtoField(FieldNumber = 5)]
    public string B { get; set; } = "";          // explicit -> field 5

    public int C { get; set; }                   // auto -> field 2
    public int D { get; set; }                   // auto -> field 3
}
```

The two-pass algorithm:
1. **Pass 1** — collect all explicitly assigned field numbers into a reserved set
2. **Pass 2** — auto-assign starting from 1, skipping any reserved numbers

This guarantees no collisions regardless of how explicit and auto-assigned fields are interleaved.

## Type Mapping

| CLR Type | Wire Type | Notes |
|----------|-----------|-------|
| `bool`, `byte`, `sbyte` | Varint | |
| `short`, `ushort`, `int`, `uint` | Varint | |
| `enum` | Varint | Underlying integer value |
| `long`, `ulong` | Fixed64 | |
| `double` | Fixed64 | |
| `float` | Fixed32 | |
| `string` | LengthDelimited | UTF-8 encoded |
| `byte[]` | LengthDelimited | Raw bytes |
| `T?` (nullable value type) | Same as `T` | Null values are skipped |
| Nested `[ProtoContract]` object | LengthDelimited | Recursively encoded |
| `T[]`, `List<T>`, collections | LengthDelimited | See Collections below |
| `Dictionary<K,V>` with `[ProtoMap]` | LengthDelimited | Map entry messages |

## Collections

Any property whose type implements `IEnumerable<T>` (excluding `string` and `byte[]`) is treated as a **repeated field**.

**Packed encoding** is used for scalar element types (`int`, `double`, `bool`, `enum`, etc.) — all values are concatenated into a single length-delimited blob, matching proto3 packed repeated fields.

**Non-packed** (one tag per element) is used for `string`, `byte[]`, and nested message element types.

Supported collection types for decoding: `T[]`, `List<T>`, `IList<T>`, `ICollection<T>`, `IReadOnlyList<T>`, `HashSet<T>`, `ISet<T>`.

```csharp
[ProtoContract]
public class Metrics
{
    public List<string> Labels { get; set; } = [];     // repeated, non-packed
    public int[] Values { get; set; } = [];             // repeated, packed
    public List<DataPoint> Points { get; set; } = [];   // repeated nested messages
}
```

## Nullable Types

Nullable value types (`int?`, `double?`, `bool?`, etc.) are fully supported. When the value is `null`, the field is omitted from the output. On decode, the property retains its default (`null`).

```csharp
[ProtoContract]
public class Reading
{
    public double? Temperature { get; set; }   // omitted when null
    public int? SensorId { get; set; }
}
```

## Streaming (Delimited Messages)

To send or receive multiple messages over a single stream, use length-delimited encoding. Each message is prefixed with a varint-encoded byte length.

### Writing

```csharp
using var stream = new MemoryStream();

// Synchronous
ProtobufEncoder.WriteDelimitedMessage(person1, stream);
ProtobufEncoder.WriteDelimitedMessage(person2, stream);

// Asynchronous
await ProtobufEncoder.WriteDelimitedMessageAsync(person3, stream, cancellationToken);
```

### Reading

```csharp
stream.Position = 0;

// Read one message (returns null at end of stream)
var msg = ProtobufEncoder.ReadDelimitedMessage<Person>(stream);

// Read all messages as IEnumerable<T>
foreach (var person in ProtobufEncoder.ReadDelimitedMessages<Person>(stream))
{
    Console.WriteLine(person.Name);
}

// Async streaming with IAsyncEnumerable<T>
await foreach (var person in ProtobufEncoder.ReadDelimitedMessagesAsync<Person>(stream))
{
    Console.WriteLine(person.Name);
}
```

## Static Messages (Pre-compiled)

For hot paths, create a static message to eagerly resolve and cache all type metadata:

```csharp
// Create once, reuse many times
var message = ProtobufEncoder.CreateStaticMessage<Person>();

byte[] bytes = message.Encode(person);
Person decoded = message.Decode(bytes);

// Also supports delimited streaming
message.WriteDelimited(person, stream);
Person? next = message.ReadDelimited(stream);
```

Standalone delegates:

```csharp
Func<Person, byte[]> encode = ProtobufEncoder.CreateStaticEncoder<Person>();
Func<byte[], Person> decode = ProtobufEncoder.CreateStaticDecoder<Person>();
```

## Full Example

```csharp
using ProtobuffEncoder;
using ProtobuffEncoder.Attributes;

public enum ContactType { Unknown, Personal, Work }

[ProtoContract]
public class Person
{
    public string Name { get; set; } = "";

    [ProtoField(FieldNumber = 10, Name = "email_address")]
    public string Email { get; set; } = "";

    public int Age { get; set; }
    public double? Score { get; set; }
    public ContactType Type { get; set; }
    public List<string> Tags { get; set; } = [];
    public int[] LuckyNumbers { get; set; } = [];
    public Address? HomeAddress { get; set; }
    public List<PhoneNumber> PhoneNumbers { get; set; } = [];

    [ProtoIgnore]
    public string InternalNotes { get; set; } = "";
}

[ProtoContract]
public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public int ZipCode { get; set; }
}

[ProtoContract]
public class PhoneNumber
{
    public string Number { get; set; } = "";
    public ContactType Type { get; set; }
}

// Usage
var person = new Person
{
    Name = "Alice",
    Email = "alice@example.com",
    Age = 30,
    Score = 98.5,
    Type = ContactType.Work,
    Tags = ["developer", "lead"],
    LuckyNumbers = [7, 13, 42],
    HomeAddress = new Address { Street = "123 Main St", City = "Springfield", ZipCode = 62701 },
    PhoneNumbers =
    [
        new PhoneNumber { Number = "+1-555-0100", Type = ContactType.Work },
        new PhoneNumber { Number = "+1-555-0101", Type = ContactType.Personal }
    ]
};

byte[] encoded = ProtobufEncoder.Encode(person);
var decoded = ProtobufEncoder.Decode<Person>(encoded);
```
