# Schema Generation & Decoding

ProtobuffEncoder can auto-generate `.proto` schema files from your C# types, and decode protobuf binary using only those schemas — no C# types needed on the receiving end.

This enables the **shared contract** pattern: define types in C#, generate `.proto` files, and any consumer (even one with zero knowledge of your C# classes) can decode the messages.

## Generating .proto Schemas

### Programmatic

```csharp
using ProtobuffEncoder.Schema;

// Generate .proto content for a single type
string proto = ProtoSchemaGenerator.Generate(typeof(WeatherRequest));

// Generate for all [ProtoContract] types in an assembly
var allProto = ProtoSchemaGenerator.GenerateAll(assembly);

// Generate to disk (one .proto file per namespace)
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
1. `assembly-path` — path to the compiled DLL containing `[ProtoContract]` types
2. `proto-output-dir` — directory to write `.proto` files
3. `csproj-path` (optional) — `.csproj` file to auto-append `<Content>` entries for the generated proto files

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

### Generated Output

Given these C# types:

```csharp
[ProtoContract]
public class WeatherRequest
{
    public string City { get; set; } = "";
    public int Days { get; set; }
    public bool IncludeHourly { get; set; }
}
```

The generator produces:

```proto
syntax = "proto3";
package ProtobuffEncoder.Contracts;

message WeatherRequest {
  string City = 1;
  int32 Days = 2;
  bool IncludeHourly = 3;
}
```

The generator handles:
- Nested messages
- Enums
- Repeated fields
- Optional fields (nullable types)
- Map fields (`[ProtoMap]`)
- OneOf groups (`[ProtoOneOf]`)
- Deprecated annotations (`[ProtoField(IsDeprecated = true)]`)

## Parsing .proto Files

```csharp
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

```csharp
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

```csharp
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

```csharp
using ProtobuffEncoder.Schema;

var writer = new ProtobufWriter();
writer.WriteString(1, "Amsterdam");        // string field
writer.WriteVarint(2, 3);                  // int32 field
writer.WriteBool(3, true);                 // bool field
writer.WriteDouble(4, 52.3676);            // double field
writer.WriteFloat(5, 4.89f);              // float field
writer.WriteFixed64(6, timestamp);         // fixed64 field
writer.WriteBytes(7, rawData);             // bytes field

// Nested messages
var inner = new ProtobufWriter();
inner.WriteString(1, "2026-03-12");
inner.WriteDouble(2, 5.3);

var outer = new ProtobufWriter();
outer.WriteMessage(1, inner);                          // single nested message
outer.WriteRepeatedMessage(2, [inner1, inner2]);       // repeated messages
outer.WriteRepeatedString(3, ["a", "b", "c"]);         // repeated strings
outer.WritePackedVarints(4, [1L, 2L, 3L]);             // packed repeated ints

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

```csharp
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
