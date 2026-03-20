# Schema Decoding

ProtobuffEncoder provides tools for working with protobuf data using only `.proto` schema definitions, without needing compiled C# types. This enables scenarios like dynamic message inspection, cross-language interop debugging, and gateway services.

## ProtoSchemaParser

Parses `.proto` file content into `ProtoFile` models.

```C#
using ProtobuffEncoder.Schema;

// Parse from string
ProtoFile file = ProtoSchemaParser.Parse(protoContent);

// Parse from file
ProtoFile file = ProtoSchemaParser.ParseFile("path/to/schema.proto");

// Parse all .proto files in a directory
List<ProtoFile> files = ProtoSchemaParser.ParseDirectory("./protos");
```

### Supported Syntax

| Feature | Parsed |
|---------|--------|
| `syntax = "proto3";` | Syntax version |
| `package name;` | Package name |
| `message Name { ... }` | Messages with nested support |
| `enum Name { ... }` | Enums with values |
| `repeated type name = N;` | Repeated fields |
| `optional type name = N;` | Optional fields |
| `map<K, V> name = N;` | Map fields |
| `oneof name { ... }` | OneOf groups |
| `[deprecated = true]` | Deprecated annotation |
| Nested messages/enums | Recursive parsing |

## SchemaDecoder

Decodes protobuf binary payloads using parsed `.proto` definitions. Returns `DecodedMessage` instances with fields accessible by name.

### Creating a Decoder

```C#
// From .proto content string
var decoder = SchemaDecoder.FromProtoContent(protoContent);

// From a .proto file
var decoder = SchemaDecoder.FromFile("schema.proto");

// From a directory of .proto files
var decoder = SchemaDecoder.FromDirectory("./protos");

// From parsed ProtoFile objects
var decoder = new SchemaDecoder(file1, file2, file3);
```

### Decoding Messages

```C#
byte[] binaryData = ReceiveFromNetwork();

DecodedMessage message = decoder.Decode("OrderMessage", binaryData);

// Access fields by name
string orderId = (string)message["OrderId"];
long total = (long)message["Total"];

// Nested messages are DecodedMessage instances
var customer = (DecodedMessage)message["Customer"];
string name = (string)customer["Name"];

// Lists
var items = (List<object?>)message["Items"];
foreach (DecodedMessage item in items.Cast<DecodedMessage>())
    Console.WriteLine(item["ProductName"]);

// Maps
var settings = (Dictionary<object, object?>)message["Settings"];
```

### Registering Additional Schemas

```C#
var decoder = new SchemaDecoder();
decoder.Register(ProtoSchemaParser.ParseFile("base.proto"));
decoder.Register(ProtoSchemaParser.ParseFile("orders.proto"));
```

### Introspection

```C#
// List all registered message types
IReadOnlyCollection<string> messages = decoder.RegisteredMessages;

// List all registered enum types
IReadOnlyCollection<string> enums = decoder.RegisteredEnums;

// Get a specific definition
ProtoMessageDef? msgDef = decoder.GetMessage("OrderMessage");
ProtoEnumDef? enumDef = decoder.GetEnum("OrderStatus");
```

## DecodedMessage

A dynamic message representation with field-name-based access:

```C#
public sealed class DecodedMessage
{
    public string TypeName { get; }
    public Dictionary<string, object?> Fields { get; }

    // Indexer shorthand
    public object? this[string fieldName] { get; set; }
}
```

### Type Mapping in SchemaDecoder

| Proto Type | Decoded CLR Type |
|-----------|-----------------|
| `bool` | `bool` |
| `int32`, `uint32`, `int64`, `uint64` | `long` |
| `float` | `double` |
| `double` | `double` |
| `string` | `string` |
| `bytes` | `byte[]` |
| `enum` | `string` (enum value name) |
| Nested message | `DecodedMessage` |
| Repeated field | `List<object?>` |
| Map field | `Dictionary<object, object?>` |

## ProtobufWriter

Low-level protobuf writer for building messages manually without C# contract types. Useful for dynamic message construction, testing, or interop.

```C#
var writer = new ProtobufWriter();
```

### Scalar Fields

```C#
writer.WriteVarint(1, 42);              // field 1 = 42 (varint)
writer.WriteBool(2, true);              // field 2 = true
writer.WriteString(3, "hello");         // field 3 = "hello"
writer.WriteDouble(4, 3.14);            // field 4 = 3.14
writer.WriteFloat(5, 2.71f);            // field 5 = 2.71
writer.WriteFixed64(6, 123456789L);     // field 6 = 123456789 (fixed64)
writer.WriteBytes(7, new byte[] {1,2}); // field 7 = bytes
```

### Nested Messages

```C#
var inner = new ProtobufWriter();
inner.WriteVarint(1, 100);
inner.WriteString(2, "nested");

var outer = new ProtobufWriter();
outer.WriteString(1, "root");
outer.WriteMessage(2, inner);  // field 2 = nested message
```

### Repeated Fields

```C#
// Packed varints
writer.WritePackedVarints(1, new long[] { 1, 2, 3, 4, 5 });

// Repeated strings
writer.WriteRepeatedString(2, new[] { "a", "b", "c" });

// Repeated nested messages
writer.WriteRepeatedMessage(3, new[] { inner1, inner2 });
```

### Map Fields

```C#
// map<string, string>
writer.WriteStringStringMap(1, settings);

// map<string, int64>
writer.WriteStringInt64Map(2, counts);

// map<string, message>
writer.WriteStringMessageMap(3, entries);

// map<int32, string>
writer.WriteIntStringMap(4, labels);

// Custom map entry
writer.WriteMapEntry(5,
    e => e.WriteString(1, "key"),
    e => e.WriteVarint(2, 42));
```

### Output

```C#
byte[] bytes = writer.ToByteArray();

// Decode with SchemaDecoder or ProtobufEncoder
var decoder = SchemaDecoder.FromProtoContent(proto);
var message = decoder.Decode("MyMessage", bytes);
```

## Round-Trip Example

```C#
// 1. Generate schema from C# type
string proto = ProtoSchemaGenerator.Generate(typeof(OrderMessage));

// 2. Encode with ProtobufEncoder
byte[] bytes = ProtobufEncoder.Encode(new OrderMessage { OrderId = 1, Total = 99.99 });

// 3. Decode with SchemaDecoder (no C# type needed)
var decoder = SchemaDecoder.FromProtoContent(proto);
var decoded = decoder.Decode("OrderMessage", bytes);

Console.WriteLine(decoded["OrderId"]); // 1
Console.WriteLine(decoded["Total"]);   // 99.99
```
