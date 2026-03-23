# Value Encoding and Dynamic Messages

ProtobuffEncoder provides three APIs for working with values without requiring `[ProtoContract]` classes: `ProtoEncoding` for text encoding control, `ProtoValue` for single-value encode/decode, and `ProtoMessage` for dynamic schema-less messages.

## ProtoEncoding

Wraps `System.Text.Encoding` with protobuf-aware defaults and named presets. All Unicode-capable encodings fully support emoji and supplementary Unicode planes.

```csharp
public sealed class ProtoEncoding
```

### Presets

| Preset | Encoding | Emoji Support | Description |
|--------|----------|---------------|-------------|
| `ProtoEncoding.UTF8` | UTF-8 | Yes | Protobuf default, most compact for ASCII-heavy text |
| `ProtoEncoding.UTF16` | UTF-16 LE | Yes | Native .NET string encoding |
| `ProtoEncoding.UTF16BE` | UTF-16 BE | Yes | Big-endian UTF-16 |
| `ProtoEncoding.UTF32` | UTF-32 LE | Yes | Fixed-width, simplest for random access |
| `ProtoEncoding.ASCII` | ASCII | No | 7-bit only, non-ASCII replaced with '?' |
| `ProtoEncoding.Latin1` | ISO 8859-1 | No | Western European characters |

### API

| Method | Description |
|--------|-------------|
| `FromEncoding(Encoding)` | Wrap any `System.Text.Encoding` |
| `FromName(string)` | Resolve by name ("utf-8", "utf-16", etc.) |
| `GetBytes(string)` | Encode string to bytes |
| `GetString(ReadOnlySpan<byte>)` | Decode bytes to string |
| `GetString(byte[])` | Decode byte array to string |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Short encoding name (e.g. "utf-8") |
| `Encoding` | `Encoding` | Underlying `System.Text.Encoding` |
| `SupportsEmoji` | `bool` | True for Unicode-capable encodings |

### Example

```csharp
// Use presets
var utf8 = ProtoEncoding.UTF8;
var utf16 = ProtoEncoding.UTF16;

// From name
var enc = ProtoEncoding.FromName("utf-32");

// From any System.Text.Encoding
var custom = ProtoEncoding.FromEncoding(Encoding.GetEncoding("windows-1252"));

// Check emoji support
Console.WriteLine(utf8.SupportsEmoji);  // true
Console.WriteLine(ProtoEncoding.ASCII.SupportsEmoji);  // false

// Encode/decode with emoji
byte[] bytes = utf8.GetBytes("Hello 🌍🎉");
string text = utf8.GetString(bytes); // "Hello 🌍🎉"
```

## ProtoValue

Encodes and decodes single primitive values to/from protobuf binary format without requiring a `[ProtoContract]` class. Each value is written as a single protobuf field (field number 1).

```csharp
public static class ProtoValue
```

### Supported Types

| Category | Encode Method | Decode Method |
|----------|--------------|---------------|
| **String** | `Encode(string, ProtoEncoding?)` | `DecodeString(ReadOnlySpan<byte>, ProtoEncoding?)` |
| **Boolean** | `Encode(bool)` | `DecodeBool(ReadOnlySpan<byte>)` |
| **Int32** | `Encode(int)` | `DecodeInt32(ReadOnlySpan<byte>)` |
| **UInt32** | `Encode(uint)` | `DecodeUInt32(ReadOnlySpan<byte>)` |
| **Int64** | `Encode(long)` | `DecodeInt64(ReadOnlySpan<byte>)` |
| **UInt64** | `Encode(ulong)` | `DecodeUInt64(ReadOnlySpan<byte>)` |
| **Float** | `Encode(float)` | `DecodeFloat(ReadOnlySpan<byte>)` |
| **Double** | `Encode(double)` | `DecodeDouble(ReadOnlySpan<byte>)` |
| **Decimal** | `Encode(decimal)` | `DecodeDecimal(ReadOnlySpan<byte>, ProtoEncoding?)` |
| **DateTime** | `Encode(DateTime)` | `DecodeDateTime(ReadOnlySpan<byte>)` |
| **DateTimeOffset** | `Encode(DateTimeOffset)` | `DecodeDateTimeOffset(ReadOnlySpan<byte>)` |
| **TimeSpan** | `Encode(TimeSpan)` | `DecodeTimeSpan(ReadOnlySpan<byte>)` |
| **DateOnly** | `Encode(DateOnly)` | `DecodeDateOnly(ReadOnlySpan<byte>)` |
| **TimeOnly** | `Encode(TimeOnly)` | `DecodeTimeOnly(ReadOnlySpan<byte>)` |
| **Guid** | `Encode(Guid)` | `DecodeGuid(ReadOnlySpan<byte>)` |
| **Bytes** | `Encode(byte[])` | `DecodeBytes(ReadOnlySpan<byte>)` |
| **Short** | `Encode(short)` | via `DecodeInt32` |
| **Byte** | `Encode(byte)` | via `DecodeInt32` |
| **Half** | `Encode(Half)` | via `DecodeBytes` |

### Example

```csharp
// Encode values — no contract class needed
byte[] strBytes = ProtoValue.Encode("Hello 🌍🎉");
byte[] intBytes = ProtoValue.Encode(42);
byte[] boolBytes = ProtoValue.Encode(true);
byte[] dateBytes = ProtoValue.Encode(DateTime.UtcNow);

// Decode values
string text = ProtoValue.DecodeString(strBytes);          // "Hello 🌍🎉"
int number = ProtoValue.DecodeInt32(intBytes);             // 42
bool flag = ProtoValue.DecodeBool(boolBytes);              // true
DateTime date = ProtoValue.DecodeDateTime(dateBytes);

// Use custom encoding for strings
byte[] utf16Bytes = ProtoValue.Encode("Привет мир", ProtoEncoding.UTF16);
string decoded = ProtoValue.DecodeString(utf16Bytes, ProtoEncoding.UTF16);
```

## ProtoMessage

A dynamic, schema-less protobuf message that uses field numbers and common CLR types without requiring a `[ProtoContract]` class. Supports fluent field assignment, nested messages, and configurable string encoding.

```csharp
public sealed class ProtoMessage
```

### Construction

```csharp
// Default UTF-8 encoding
var message = new ProtoMessage();

// Custom encoding for string fields
var message = new ProtoMessage(ProtoEncoding.UTF16);
```

### Set Methods

All set methods return the message for fluent chaining.

| Method | Description |
|--------|-------------|
| `Set(int fieldNumber, string value)` | Set string field (supports emoji) |
| `Set(int fieldNumber, bool value)` | Set boolean field |
| `Set(int fieldNumber, int value)` | Set int32 field |
| `Set(int fieldNumber, uint value)` | Set uint32 field |
| `Set(int fieldNumber, long value)` | Set int64 field |
| `Set(int fieldNumber, ulong value)` | Set uint64 field |
| `Set(int fieldNumber, float value)` | Set float field |
| `Set(int fieldNumber, double value)` | Set double field |
| `Set(int fieldNumber, decimal value)` | Set decimal field |
| `Set(int fieldNumber, DateTime value)` | Set DateTime field |
| `Set(int fieldNumber, DateTimeOffset value)` | Set DateTimeOffset field |
| `Set(int fieldNumber, TimeSpan value)` | Set TimeSpan field |
| `Set(int fieldNumber, DateOnly value)` | Set DateOnly field |
| `Set(int fieldNumber, TimeOnly value)` | Set TimeOnly field |
| `Set(int fieldNumber, Guid value)` | Set Guid field |
| `Set(int fieldNumber, byte[] value)` | Set byte array field |
| `Set(int fieldNumber, ProtoMessage value)` | Set nested message field |

### Get Methods

| Method | Description |
|--------|-------------|
| `Get<T>(int fieldNumber)` | Get typed value (with numeric conversion) |
| `GetString(int fieldNumber)` | Get string value |
| `GetInt32(int fieldNumber)` | Get int32 value |
| `GetInt64(int fieldNumber)` | Get int64 value |
| `GetBool(int fieldNumber)` | Get boolean value |
| `GetDouble(int fieldNumber)` | Get double value |
| `GetFloat(int fieldNumber)` | Get float value |
| `GetDateTime(int fieldNumber)` | Get DateTime value |
| `GetGuid(int fieldNumber)` | Get Guid value |
| `GetMessage(int fieldNumber)` | Get nested ProtoMessage |
| `GetRaw(int fieldNumber)` | Get raw value as object |
| `HasField(int fieldNumber)` | Check if field exists |
| `Remove(int fieldNumber)` | Remove a field |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `FieldCount` | `int` | Number of fields set |
| `FieldNumbers` | `IReadOnlyCollection<int>` | Field numbers present |
| `Encoding` | `ProtoEncoding` | Default string encoding |

### Encode/Decode

| Method | Description |
|--------|-------------|
| `ToBytes()` | Encode to protobuf binary |
| `WriteTo(Stream)` | Write to stream |
| `WriteDelimitedTo(Stream)` | Write with length prefix (for transport) |
| `WriteDelimitedToAsync(Stream, CancellationToken)` | Async delimited write |
| `FromBytes(ReadOnlySpan<byte>, ProtoEncoding?)` | Decode from binary |
| `ReadDelimitedFrom(Stream, ProtoEncoding?)` | Read one from stream (null at EOF) |
| `ReadAllDelimitedFrom(Stream, ProtoEncoding?)` | Read all from stream |
| `ReadAllDelimitedFromAsync(Stream, ProtoEncoding?, CancellationToken)` | Async read all |

### Example

```csharp
// Build a message with fluent API
var msg = new ProtoMessage()
    .Set(1, "Hello 🌍")
    .Set(2, 42)
    .Set(3, true)
    .Set(4, DateTime.UtcNow)
    .Set(5, Guid.NewGuid());

// Nested messages
var address = new ProtoMessage()
    .Set(1, "123 Main St")
    .Set(2, "Amsterdam")
    .Set(3, "NL");

var person = new ProtoMessage()
    .Set(1, "Jan")
    .Set(2, "jan@example.com")
    .Set(3, address);

// Encode and decode
byte[] bytes = person.ToBytes();
var decoded = ProtoMessage.FromBytes(bytes);
Console.WriteLine(decoded.GetString(1));            // "Jan"
Console.WriteLine(decoded.GetMessage(3)?.GetString(2)); // "Amsterdam"

// Stream transport
await using var stream = new MemoryStream();
person.WriteDelimitedTo(stream);
stream.Position = 0;
var received = ProtoMessage.ReadDelimitedFrom(stream);
```

## Emoji and Unicode Streaming

All three APIs work together to enable full Unicode streaming including emoji:

```csharp
// Sender side
await using var sender = new ProtobufValueSender(networkStream, ProtoEncoding.UTF8);
await sender.SendAsync("Hello 🌍🎉");
await sender.SendAsync("日本語テスト 🇯🇵");
await sender.SendAsync("Emoji sequence: 👨‍👩‍👧‍👦");

// Receiver side
await using var receiver = new ProtobufValueReceiver(networkStream, ProtoEncoding.UTF8);
await foreach (var text in receiver.ReceiveAllStringsAsync(ct))
    Console.WriteLine(text);
// Output:
// Hello 🌍🎉
// 日本語テスト 🇯🇵
// Emoji sequence: 👨‍👩‍👧‍👦

// Or use ProtoMessage for structured emoji content
var chat = new ProtoMessage(ProtoEncoding.UTF8)
    .Set(1, "user123")
    .Set(2, "Great work! 🎊👏🔥")
    .Set(3, DateTime.UtcNow);

await sender.Send(chat);
```

## Per-Field Encoding in Contracts

Use `[ProtoContract(DefaultEncoding)]` and `[ProtoField(Encoding)]` to control text encoding in contract classes:

```csharp
[ProtoContract(DefaultEncoding = "utf-8")]
public class LocalizedContent
{
    [ProtoField(1)] public string Title { get; set; } = "";           // uses utf-8
    [ProtoField(2)] public string Body { get; set; } = "";            // uses utf-8
    [ProtoField(3, Encoding = "utf-16")] public string RichText { get; set; } = ""; // utf-16
    [ProtoField(4, Encoding = "ascii")] public string Code { get; set; } = "";      // ascii
}

// Encode/decode with encoding-aware fields
var content = new LocalizedContent
{
    Title = "🎯 Goals",
    Body = "Set your targets! 🏆",
    RichText = "Formatted content with emoji 🎨",
    Code = "SELECT * FROM users"
};

byte[] bytes = ProtobufEncoder.Encode(content);
var decoded = ProtobufEncoder.Decode<LocalizedContent>(bytes);
```
