# Streaming & Transport Benchmarks

This section covers the performance of ProtobuffEncoder's streaming capabilities, including length-delimited framing, bidirectional duplex streams, and asynchronous operations.

## Length-Delimited Streaming

Tests throughput for reading and writing batches of 100 messages with length-delimited framing.

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **WriteDelimited_100** | 68.369 μs | 2.3667 μs | 6.3477 | 79.69 KB |
| **ReadDelimited_100** | 59.625 μs | 2.9521 μs | 5.8594 | 73.39 KB |
| **SenderReceiver_RoundTrip** | 1.362 μs | 0.0538 μs | 0.1526 | 1.88 KB |

**Key Insight:** Batch processing of delimited messages is highly efficient, with `ReadDelimited` being slightly faster than `WriteDelimited` due to optimized stream reading patterns.

## Duplex Stream Performance

Measures `ProtobufDuplexStream` performance, which involves thread-safe locking and simultaneous read/write capability.

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **DuplexStream_SendAndReceive** | 1.264 μs | 0.0135 μs | 0.1678 | 2.08 KB |
| **DuplexStream_SendMany_10** | 6.255 μs | 0.1424 μs | 0.6409 | 8.15 KB |

**Key Insight:** The overhead added by the duplex transport layer is minimal (~100-200 ns per message) compared to raw serialization, making it suitable for low-latency bidirectional communication.

## Async Operations

Tests the performance of asynchronous encoding, decoding, and streaming APIs.

| Method | Mean | StdDev | Gen0 | Allocated |
|:---|---:|---:|---:|---:|
| **EncodeAsync** | 751.2 ns | 95.63 ns | 0.0877 | 1.1 KB |
| **DecodeAsync** | 684.5 ns | 19.26 ns | 0.1030 | 1.3 KB |
| **WriteDelimitedAsync_50** | 37.504 μs | 1,884.59 ns | 4.6387 | 58.53 KB |

**Key Insight:** Async operations in .NET 10 are highly optimized, showing very low overhead compared to their synchronous counterparts when running on high-performance streams like `MemoryStream`.

