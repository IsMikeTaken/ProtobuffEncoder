# Infrastructure & Internals Benchmarks

This section covers the performance of ProtobuffEncoder's internal infrastructure, including pre-compiled messages, validation pipelines, contract resolution, and low-level writer performance.

## Static Message vs Dynamic

Compares `StaticMessage<T>` (pre-compiled delegates) against the standard dynamic `ProtobufEncoder` methods.

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **StaticEncode** | 557.9 ns | 13.43 ns | 0.0629 | 792 B |
| **StaticDecode** | 626.3 ns | 108.12 ns | 0.0591 | 744 B |
| **DynamicEncode** | 683.5 ns | 91.76 ns | 0.0610 | 792 B |
| **DynamicDecode** | 808.8 ns | 51.58 ns | 0.0591 | 744 B |

**Key Insight:** `StaticMessage` provides a ~20% performance boost by bypassing the `ContractResolver` dictionary lookup and using pre-compiled delegates for field access.

## Validation Pipeline

Measures the throughput of the validation pipeline with 3 rules.

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **Validate_Valid** | 11.210 ns | 2.3494 ns | - | - |
| **Validate_Invalid** | 6.123 ns | 0.5023 ns | 0.0025 | 32 B |
| **ValidatedSender_Send** | 692.946 ns | 41.8947 ns | 0.1106 | 1,424 B |

**Key Insight:** Validation is extremely fast (~10ns). Invalid messages are validated even faster because the pipeline short-circuits on the first failure.

## ContractResolver Caching

Tests the overhead of the `ContractResolver` when types are already cached.

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **FirstCall_NewType_Encode** | 821.8 ns | 26.12 ns | 0.0639 | 808 B |
| **CachedResolve_AllScalars** | 1,100.2 ns | 35.77 ns | 0.0782 | 1,000 B |
| **CachedResolve_Nested** | 1,192.4 ns | 11.31 ns | 0.1259 | 1,592 B |

**Key Insight:** The `ContractResolver` adds minimal overhead once a type is resolved, as it uses a `ConcurrentDictionary` for O(1) retrieval of pre-computed type metadata.

## Low-Level ProtobufWriter

Benchmarks the manual message construction using the low-level `ProtobufWriter` API.

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **Writer_SimpleMessage** | 76.30 ns | 1.946 ns | 0.0356 | 448 B |
| **Writer_NestedMessage** | 144.57 ns | 26.778 ns | 0.0694 | 872 B |
| **Writer_MapField** | 1,773.07 ns | 148.032 ns | 0.5951 | 7,472 B |
| **Writer_PackedVarints** | 388.86 ns | 70.844 ns | 0.0720 | 904 B |

**Key Insight:** For performance-critical code where object allocation must be avoided, the `ProtobufWriter` provides the fastest possible path to generating Protobuf-compliant binary data.

