# Schema Operations Benchmarks

This section covers the performance of dynamic schema-related operations, including .proto generation, schema parsing, and schema-based decoding.

## Schema Generation

Measures the time to generate a `.proto` schema string from C# types using reflection.

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **Generate_SimpleMessage** | 3.125 μs | 0.3192 μs | 0.3204 | 3.94 KB |
| **Generate_NestedMessage** | 5.995 μs | 0.4600 μs | 0.5798 | 7.12 KB |
| **Generate_AllScalars** | 4.311 μs | 0.0475 μs | 0.4578 | 5.78 KB |
| **Generate_WithOneOf** | 3.828 μs | 0.0833 μs | 0.3662 | 4.59 KB |
| **Generate_WithMap** | 4.198 μs | 0.0807 μs | 0.3052 | 3.84 KB |

**Key Insight:** Schema generation is a reflection-heavy operation. While not intended for hot-path use, it is efficient enough to be used for dynamic discovery or on-the-fly schema serving.

## Parsing & Schema Decoding

Measures the performance of the `ProtoSchemaParser` and the `SchemaDecoder` (which decodes binary data without C# types).

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **Parse_Proto** | 1,470.0 ns | 75.29 ns | 0.3929 | 4,952 B |
| **SchemaDecoder_Decode** | 126.1 ns | 7.96 ns | 0.0553 | 696 B |

**Key Insight:** Once a schema is parsed, the `SchemaDecoder` is extremely fast at extracting data from binary Protobuf messages, making it an excellent choice for generic proxies or data inspection tools where C# models are not available.

