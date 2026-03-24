# ProtobuffEncoder Benchmarks

This directory contains comprehensive benchmark results for ProtobuffEncoder, covering core performance, collection handling, streaming, and infrastructure.

All benchmarks are performed using [BenchmarkDotNet](https://benchmarkdotnet.org/) across .NET 8, 9, and 10.

## Benchmark Categories

1.  **[Core Performance](core_performance1.md)**
    *   Basic Encode/Decode (Small vs Large messages)
    *   Payload Scaling (100B to 100KB)
    *   Nesting Depth impact
2.  **[Collections & Advanced Types](collections_and_types1.md)**
    *   Lists and Maps performance
    *   OneOf union groups
    *   Inheritance ([ProtoInclude])
3.  **[Streaming & Transport](streaming_and_transport1.md)**
    *   Length-delimited framing
    *   Bidirectional Duplex Streams
    *   Asynchronous operations
4.  **[Infrastructure & Internals](infrastructure1.md)**
    *   Pre-compiled Static Messages
    *   Validation Pipeline throughput
    *   ContractResolver caching
    *   Low-level ProtobufWriter
5.  **[Schema Operations](schema_operations1.md)**
    *   Dynamic .proto Generation
    *   Schema Parsing and Schema-based decoding

## Running Benchmarks Locally

To run the benchmarks yourself:

```bash
dotnet run -c Release --project benchmarks/ProtobuffEncoder.Benchmarks
```

You can also filter for specific benchmarks:

```bash
dotnet run -c Release --project benchmarks/ProtobuffEncoder.Benchmarks -- --filter "*EncoderBenchmarks*"
```

