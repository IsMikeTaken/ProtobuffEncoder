# Benchmarks

ProtobuffEncoder includes a comprehensive benchmark suite using [BenchmarkDotNet](https://benchmarkdotnet.org/) with **15 benchmark classes** covering every major feature. All benchmarks run across **.NET 8, 9, and 10** for cross-runtime comparison.

## Running Benchmarks

```bash
# Run all benchmarks (takes ~30 minutes)
dotnet run -c Release --project benchmarks/ProtobuffEncoder.Benchmarks

# Run specific benchmark class
dotnet run -c Release --project benchmarks/ProtobuffEncoder.Benchmarks -- --filter "*EncoderBenchmarks*"

# List available benchmarks
dotnet run -c Release --project benchmarks/ProtobuffEncoder.Benchmarks -- --list flat
```

## Benchmark Suites

### 1. EncoderBenchmarks -- Core Encode/Decode

Measures the fundamental encode and decode operations for small (2 fields) and large (7 fields + 1KB payload) messages.

| Method | net10.0 Mean | Allocated |
|--------|-------------|-----------|
| Encode_Small | ~600 ns | 792 B |
| Decode_Small | ~530 ns | 736 B |
| Encode_Large | ~1,000 ns | 6,832 B |
| Decode_Large | ~820 ns | 3,832 B |

**Key insight**: Encoding scales linearly with payload size. Decode is faster than encode due to pre-allocated target objects.

### 2. CollectionBenchmarks -- Lists and Maps

Benchmarks serialization of `List<int>` (100 items), `List<string>` (50 items), and `Dictionary<string, string>` (100 entries).

| Method | Description |
|--------|-------------|
| `Encode_List` | Packed int list + non-packed string list |
| `Decode_List` | Reverse decode with list construction |
| `Encode_Map` | 100 map entries with string keys/values |
| `Decode_Map` | Map reconstruction from wire format |

### 3. StaticMessageBenchmarks -- Pre-compiled vs Dynamic

Compares `StaticMessage<T>` (pre-resolved descriptors) against dynamic `ProtobufEncoder.Encode/Decode`.

| Method | Description |
|--------|-------------|
| `StaticEncode` | Pre-compiled encode delegate |
| `StaticDecode` | Pre-compiled decode delegate |
| `DynamicEncode` | Standard `ProtobufEncoder.Encode` |
| `DynamicDecode` | Standard `ProtobufEncoder.Decode` |

**Note**: Due to internal caching in `ContractResolver`, the static path primarily saves the dictionary lookup overhead.

### 4. StreamingBenchmarks -- Length-Delimited Framing

Tests throughput for streaming 100 messages with length-delimited framing.

| Method | Description |
|--------|-------------|
| `WriteDelimited_100` | Write 100 length-delimited messages |
| `ReadDelimited_100` | Read 100 length-delimited messages |
| `SenderReceiver_RoundTrip` | Full Sender -> Receiver pipeline for 1 message |

### 5. DuplexStreamBenchmarks -- Bidirectional Transport

Measures `ProtobufDuplexStream` performance with thread-safe locking overhead.

| Method | Description |
|--------|-------------|
| `DuplexStream_SendAndReceive` | Single send + receive cycle |
| `DuplexStream_SendMany_10` | 10 sequential sends through duplex |

### 6. ValidationBenchmarks -- Pipeline Throughput

Measures validation pipeline with 3 rules (predicate-based).

| Method | Description |
|--------|-------------|
| `Validate_Valid` | Validate a message that passes all rules |
| `Validate_Invalid` | Validate a message that fails the first rule |
| `ValidatedSender_Send` | Full validated send through `ValidatedProtobufSender` |

**Key insight**: Validation short-circuits on first failure, making invalid message validation faster.

### 7. SchemaGenerationBenchmarks -- .proto Generation

Measures `ProtoSchemaGenerator.Generate()` for different type complexities.

| Method | Description |
|--------|-------------|
| `Generate_SimpleMessage` | 2 fields |
| `Generate_NestedMessage` | Message with nested message field |
| `Generate_AllScalars` | 7 fields of different types |
| `Generate_WithOneOf` | OneOf group generation |
| `Generate_WithMap` | Map field generation |

### 8. SchemaParsingBenchmarks -- .proto Parsing

Measures `ProtoSchemaParser.Parse()` and `SchemaDecoder.Decode()`.

| Method | Description |
|--------|-------------|
| `Parse_Proto` | Parse a generated .proto string |
| `SchemaDecoder_Decode` | Decode binary data using schema (no C# types) |

### 9. ProtobufWriterBenchmarks -- Low-Level API

Benchmarks the `ProtobufWriter` for manual message construction.

| Method | Description |
|--------|-------------|
| `Writer_SimpleMessage` | Write varint + string fields |
| `Writer_NestedMessage` | Write nested message |
| `Writer_MapField` | Write 10 map entries |
| `Writer_PackedVarints` | Write packed repeated varints (100 values) |

### 10. PayloadScalingBenchmarks -- Size Impact

Tests how encode/decode time scales with payload size.

| Method | Payload Size | Description |
|--------|-------------|-------------|
| `Encode_100B` | ~100 bytes | Baseline |
| `Encode_10KB` | ~10 KB | Medium payload |
| `Encode_100KB` | ~100 KB | Large payload |
| `Decode_100B` | ~100 bytes | Baseline decode |
| `Decode_10KB` | ~10 KB | Medium decode |
| `Decode_100KB` | ~100 KB | Large decode |

### 11. NestedObjectBenchmarks -- Depth Impact

Tests how nesting depth affects performance.

| Method | Depth | Description |
|--------|-------|-------------|
| `Encode_Shallow` | 1 level | Simple nested message |
| `Decode_Shallow` | 1 level | Simple nested decode |
| `Encode_Deep_3Levels` | 3 levels | Root -> L1 -> L2 -> L3 |
| `Decode_Deep_3Levels` | 3 levels | Deep nested decode |

### 12. OneOfBenchmarks -- Union Encoding

Tests oneOf group encoding where only one field is written.

| Method | Description |
|--------|-------------|
| `Encode_OneOf_Email` | OneOf with email set |
| `Encode_OneOf_Phone` | OneOf with phone set |
| `Decode_OneOf_Email` | Decode with email field |
| `Decode_OneOf_Phone` | Decode with phone field |

### 13. InheritanceBenchmarks -- ProtoInclude

Tests polymorphic encoding with `[ProtoInclude]`.

| Method | Description |
|--------|-------------|
| `Encode_DerivedType` | Encode derived type with base + derived fields |
| `Decode_DerivedType` | Decode and reconstruct derived type |

### 14. AsyncStreamingBenchmarks -- Async Operations

Tests the async encode/decode and streaming APIs.

| Method | Description |
|--------|-------------|
| `EncodeAsync` | `ProtobufEncoder.EncodeAsync()` |
| `DecodeAsync` | `ProtobufEncoder.DecodeAsync<T>()` |
| `WriteDelimitedAsync_50` | Write 50 async delimited messages |

### 15. ContractResolverBenchmarks -- Caching Impact

Tests the cached ContractResolver path for different type complexities.

| Method | Description |
|--------|-------------|
| `FirstCall_NewType_Encode` | Encode simple type (cached path) |
| `CachedResolve_AllScalars` | Encode 7-field type (cached path) |
| `CachedResolve_Nested` | Encode nested type (cached path) |

## Existing Results

### EncoderBenchmarks (net10.0)

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.7840)
12th Gen Intel Core i9-12900H 2.50GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.104

| Method       | Mean     | Error      | StdDev   | Gen0   | Gen1   | Allocated |
|------------- |---------:|-----------:|---------:|-------:|-------:|----------:|
| Encode_Small | 602.6 ns | 1,784.5 ns | 97.82 ns | 0.0610 |      - |     792 B |
| Decode_Small | 529.1 ns |   264.2 ns | 14.48 ns | 0.0572 |      - |     736 B |
| Encode_Large | 999.0 ns |   639.3 ns | 35.04 ns | 0.5417 | 0.0038 |    6832 B |
| Decode_Large | 820.5 ns |   669.7 ns | 36.71 ns | 0.3052 |      - |    3832 B |
```

### CollectionBenchmarks (net10.0)

```
| Method             | Mean     | Error    | StdDev    | Gen0   | Allocated |
|------------------- |---------:|---------:|----------:|-------:|----------:|
| Encode_Collections | 2.518 us | 2.686 us | 0.1472 us | 0.4921 |   6.07 KB |
| Decode_Collections | 4.039 us | 2.034 us | 0.1115 us | 0.9460 |  11.67 KB |
```

## Performance Characteristics

| Operation | Complexity | Notes |
|-----------|-----------|-------|
| Encode scalar | O(1) | Single varint/fixed write |
| Encode string | O(n) | UTF-8 encoding + length prefix |
| Encode collection | O(n) | Per-element encoding |
| Encode map | O(n) | Per-entry message wrapping |
| Encode nested | O(depth) | Recursive, temporary MemoryStream per level |
| ContractResolver | O(1) amortized | ConcurrentDictionary lookup after first resolve |
| Validation | O(rules) | Short-circuits on first failure |
| Schema generation | O(fields) | Reflection + string building |

## Memory Profile

- **No allocations** during varint/fixed encoding (writes directly to stream)
- **MemoryStream per encode** for byte array output
- **One allocation per nested message** (temporary MemoryStream)
- **ContractResolver caches** are never evicted (designed for long-running services)
- **StaticMessage** caches delegate references, avoiding dictionary lookups
