# Serialization Deep Dive

## How Encoding Works

`ProtobufEncoder.Encode()` follows these steps:

1. **Resolve descriptors** -- `ContractResolver.Resolve(type)` uses reflection to discover `[ProtoField]` properties, caching the result for future calls
2. **Validate required fields** -- Any field with `IsRequired = true` must have a non-default value
3. **Handle oneOf groups** -- Only the first non-default property in each group is written
4. **Encode each field** -- Tag (field number + wire type) followed by the value:
   - **Varint** fields: variable-length integer encoding
   - **Fixed32/Fixed64**: raw 4 or 8 byte little-endian
   - **LengthDelimited**: varint length prefix + payload bytes
5. **Handle ProtoInclude** -- Derived type fields encoded as nested messages at the include field number

## Type Mapping

### CLR to Proto Type Mapping

| CLR Type | Proto Type | Wire Type | Notes |
|----------|-----------|-----------|-------|
| `bool` | `bool` | Varint | 0 or 1 |
| `byte`, `sbyte` | `uint32` / `int32` | Varint | |
| `short`, `ushort` | `int32` / `uint32` | Varint | |
| `int` | `int32` | Varint | |
| `uint` | `uint32` | Varint | |
| `long` | `int64` | Fixed64 | |
| `ulong` | `uint64` | Fixed64 | |
| `nint`, `nuint` | `int32` / `uint32` | Varint | |
| `float` | `float` | Fixed32 | |
| `double` | `double` | Fixed64 | |
| `string` | `string` | LengthDelimited | UTF-8 |
| `byte[]` | `bytes` | LengthDelimited | |
| `DateTime` | Fixed64 | Fixed64 | Stored as `Ticks` |
| `TimeSpan` | Fixed64 | Fixed64 | Stored as `Ticks` |
| `DateTimeOffset` | bytes (16) | LengthDelimited | 8 bytes ticks + 8 bytes offset |
| `DateOnly` | bytes (4) | LengthDelimited | `DayNumber` as int32 |
| `TimeOnly` | bytes (8) | LengthDelimited | `Ticks` as int64 |
| `Guid` | bytes (16) | LengthDelimited | `ToByteArray()` |
| `decimal` | string | LengthDelimited | Invariant culture string |
| `Version` | string | LengthDelimited | `ToString()` |
| `Uri` | string | LengthDelimited | `AbsoluteUri` |
| `Int128` | bytes (16) | LengthDelimited | Little-endian |
| `UInt128` | bytes (16) | LengthDelimited | Little-endian |
| `Half` | bytes (2) | LengthDelimited | `BitConverter` |
| `BigInteger` | bytes | LengthDelimited | `ToByteArray()` |
| `Complex` | bytes (16) | LengthDelimited | 8 bytes real + 8 bytes imaginary |
| `enum` | varint | Varint | `Convert.ToUInt64()` |

### Collection Encoding

**Packed repeated fields** (proto3 default for scalar types):

```
[tag: field_number << 3 | 2] [varint: total_length] [value1] [value2] [value3]...
```

**Non-packed repeated fields** (strings, bytes, nested messages):

```
[tag] [length] [value1]
[tag] [length] [value2]
...
```

### Map Field Encoding

Each map entry is a length-delimited message with `key = field 1` and `value = field 2`:

```
[tag: field_number << 3 | 2] [length] {
  [tag: 1 << 3 | key_wire] [key_value]
  [tag: 2 << 3 | val_wire] [val_value]
}
```

## Encode API

```C#
// Sync - returns byte array
byte[] bytes = ProtobufEncoder.Encode(instance);

// Sync - writes to stream
ProtobufEncoder.Encode(instance, outputStream);

// Async - writes to stream
await ProtobufEncoder.EncodeAsync(instance, outputStream, cancellationToken);

// Length-delimited (for streaming multiple messages)
ProtobufEncoder.WriteDelimitedMessage(instance, stream);
await ProtobufEncoder.WriteDelimitedMessageAsync(instance, stream, ct);
```

## Decode API

```C#
// Sync - from byte array/span
T result = ProtobufEncoder.Decode<T>(bytes);
object result = ProtobufEncoder.Decode(typeof(T), bytes);

// Async - from stream (reads all remaining bytes)
T result = await ProtobufEncoder.DecodeAsync<T>(stream, ct);

// Length-delimited (single message, returns null at EOF)
T? msg = ProtobufEncoder.ReadDelimitedMessage<T>(stream);

// Length-delimited (all messages as IEnumerable)
foreach (var msg in ProtobufEncoder.ReadDelimitedMessages<T>(stream))
    Process(msg);

// Length-delimited (async enumerable)
await foreach (var msg in ProtobufEncoder.ReadDelimitedMessagesAsync<T>(stream, ct))
    Process(msg);
```

## Static Messages

Pre-compiled message handlers that cache reflection lookups at creation time:

```C#
// Create once, reuse many times
StaticMessage<T> msg = ProtobufEncoder.CreateStaticMessage<T>();

// Or create individual delegates
Func<T, byte[]> encoder = ProtobufEncoder.CreateStaticEncoder<T>();
Func<byte[], T> decoder = ProtobufEncoder.CreateStaticDecoder<T>();
```

### StaticMessage API

| Method | Description |
|--------|-------------|
| `Encode(T instance)` | Encode to `byte[]` |
| `Decode(byte[] data)` | Decode from `byte[]` |
| `WriteDelimited(T instance, Stream output)` | Write length-delimited to stream |
| `WriteDelimitedAsync(T instance, Stream output, CancellationToken)` | Async write |
| `ReadDelimited(Stream input)` | Read single length-delimited message |

## ContractResolver

The `ContractResolver` is an internal static class that:

1. Uses `ConcurrentDictionary` for thread-safe caching
2. Resolves `FieldDescriptor[]` for any type on first access
3. Supports both explicit (`[ProtoContract]`) and implicit mode
4. Handles inheritance chain walking when `IncludeBaseFields = true`
5. Auto-assigns field numbers that skip reserved numbers (from `[ProtoInclude]`)
6. Detects collection types, dictionary types, and scalar types

## Default Value Handling

Proto3 semantics: fields with default values are **not** written by default.

| Type | Default Value |
|------|--------------|
| Numeric types | `0` |
| `bool` | `false` |
| `string` | `""` or `null` |
| `byte[]` | empty array |
| Collections | empty collection |
| Dictionaries | empty dictionary |
| Nullable types | `null` |

Override with `[ProtoField(WriteDefault = true)]` to always write a field.

## Unknown Fields

Unknown field numbers in incoming data are **silently skipped** during decoding. This provides forward compatibility -- newer message versions with extra fields can be decoded by older code.

