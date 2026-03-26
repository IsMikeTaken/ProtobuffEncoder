# Collections & Advanced Types

This section covers the performance of ProtobuffEncoder when handling complex data structures like lists, maps, oneOf groups, and inherited types.

## Collection Handling

Benchmarks serialization of `List<int>` (100 items), `List<string>` (50 items), and `Dictionary<string, string>` (100 entries).

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **Encode_List** | 2.491 μs | 0.1474 μs | 0.4921 | 6.07 KB |
| **Decode_List** | 3.729 μs | 0.1129 μs | 0.9460 | 11.67 KB |
| **Encode_Map** | 9.246 μs | 0.2738 μs | 4.0131 | 49.25 KB |
| **Decode_Map** | 8.980 μs | 0.2164 μs | 2.1973 | 27.12 KB |

> [!NOTE]
> Map serialization is significantly more expensive than list serialization because Protobuf maps are represented as a repeated sequence of key-value messages, requiring more object allocations and length-prefixing.

## OneOf (Union) Groups

Tests performance when only one field in a `oneof` group is populated.

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **Encode_OneOf_Email** | 877.0 ns | 77.30 ns | 0.0715 | 904 B |
| **Encode_OneOf_Phone** | 603.2 ns | 16.29 ns | 0.0687 | 896 B |
| **Decode_OneOf_Email** | 549.7 ns | 9.07 ns | 0.0591 | 752 B |
| **Decode_OneOf_Phone** | 552.2 ns | 21.83 ns | 0.0591 | 744 B |

> [!TIP]
> OneOf overhead is negligible; it performs similarly to standard field serialization while providing type safety for mutually exclusive fields.

## Inheritance ([ProtoInclude])

Tests polymorphic encoding where a derived type is serialized through its base class contract.

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **Encode_DerivedType** | 3.395 μs | 229.52 ns | 0.1984 | 2,624 B |
| **Decode_DerivedType** | 588.6 ns | 16.38 ns | 0.0687 | 888 B |

> [!IMPORTANT]
> Encoding derived types is slower because it must resolve the hierarchy and wrap the derived fields in a sub-message (field 10 in this benchmark). Decoding is efficient as it uses the pre-resolved type mapping.

