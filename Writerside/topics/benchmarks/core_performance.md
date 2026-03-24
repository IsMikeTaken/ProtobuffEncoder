# Core Performance

This section covers the fundamental encoding and decoding performance of ProtobuffEncoder, including scaling with message size and nesting depth.

## Standard Encoder Benchmarks

Measures the fundamental encode and decode operations for small (2 fields) and large (7 fields + 1KB payload) messages.

| Method | Mean | StdDev | Gen0 | Gen1 | Allocated |
|:---|---:|---:|---:|---:|---:|
| **Encode_Small** | 1,663.1 ns | 23.31 ns | 0.0610 | - | 792 B |
| **Decode_Small** | 625.7 ns | 30.95 ns | 0.0572 | - | 736 B |
| **Encode_Large** | 1,400.4 ns | 100.56 ns | 0.8850 | 0.0153 | 11,177 B |
| **Decode_Large** | 1,227.0 ns | 270.06 ns | 0.3414 | 0.0038 | 4,304 B |

> [!TIP]
> Decode is significantly faster than encode for small messages. Encoding large messages involves more allocation due to internal buffering during the multi-pass encoding required for length-delimited nested structures.

## Payload Scaling

Tests how encode/decode time scales with increasingly large string and byte array payloads.

| Method | Mean | StdDev | Allocated |
|:---|---:|---:|---:|
| **Encode_100B** | 1,315.1 ns | 35.89 ns | 1.2 KB |
| **Encode_10KB** | 6,767.8 ns | 1,521.27 ns | 59.57 KB |
| **Encode_100KB** | 114,831.7 ns | 2,628.78 ns | 587.17 KB |
| **Decode_100B** | 600.1 ns | 4.91 ns | 1.02 KB |
| **Decode_10KB** | 2,807.4 ns | 951.73 ns | 30.02 KB |
| **Decode_100KB** | 31,563.9 ns | 3,161.03 ns | 293.74 KB |

> [!NOTE]
> Performance scales linearly with payload size. Decode remains consistently faster than encode even at large scales.

## Nesting Depth Impact

Tests how deep object hierarchies affect performance.

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **Encode_Shallow** | 1.249 μs | 0.0514 μs | 0.1144 | 1.48 KB |
| **Decode_Shallow** | 1.184 μs | 0.0164 μs | 0.1163 | 1.43 KB |
| **Encode_Deep (3 Levels)** | 2.413 μs | 0.0540 μs | 0.2289 | 2.96 KB |
| **Decode_Deep (3 Levels)** | 2.383 μs | 0.1026 μs | 0.2365 | 2.92 KB |

> [!IMPORTANT]
> Each level of nesting adds roughly 1.2 μs and 1.5 KB of allocation overhead during serialization due to recursive processing and length-prefix calculation.

