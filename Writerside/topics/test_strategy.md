# Test Strategy

This document outlines the comprehensive testing strategy for the `ProtobuffEncoder` library and its ecosystem. We utilize a multi-layered approach following **F.I.R.S.T-U** principles and advanced testing patterns to ensure maximum reliability and performance.

## F.I.R.S.T-U Principles
- **Fast**: Tests are optimized to run in milliseconds, ensuring rapid feedback.
- **Independent**: Each test is self-contained with no shared state or side effects.
- **Repeatable**: Deterministic results across any environment (.NET 8/9/10).
- **Self-validating**: Clear pass/fail results without manual inspection.
- **Thorough**: Covers happy paths, boundaries, and extreme "break it" scenarios.
- **Unit**: Focuses on individual components with strict mocking of dependencies.

## Advanced Testing Patterns

### Pass/Fail Patterns
- **Simple-Test**: Validates core functionality, constructor defaults, and nominal flows.
- **Code-Path**: Ensures branch coverage across complex logic (wire types, method types, formatters).
- **Parameter-Range**: Tests extreme numeric values, string lengths, and scaling payloads.

### Data & Simulation Patterns
- **Simple-Data-I/O**: Verifies binary serialization round-trips, stream encoding, and formatter input/output.
- **Constraint-Data**: Tests schema constraints, empty contracts, missing field behavior, and validation boundaries.
- **Rollback**: Validates recovery after failed decodes, stream errors, and large allocations.
- **Mock-Object**: Uses `FakeItEasy` and custom test doubles (e.g., `FakeWebSocket`) to simulate dependencies.
- **Service-Simulation**: Tests validation pipelines as service input boundaries.
- **Bit-Error-Simulation**: Injects malformed binary data, random bytes, and truncated messages.
- **Component-Simulation**: Full pipeline round-trips with ASP.NET Core TestHost (HTTP + WebSocket).

### Collection & Performance Patterns
- **Collection-Order**: Ensures list, map, and strategy registration ordering is preserved.
- **Collection-Indexing**: Verifies lookup by ID and direct element access.
- **Collection-Constraint**: Tests uniqueness enforcement, empty collections, and duplicate prevention.
- **Enumeration**: Validates snapshot semantics, async streams, and iterator cancellation.
- **Bulk-Data-Stress-Test**: Stress-tests large payloads (10MB+) and 100k+ collection items.
- **Performance-Test**: Execution-time assertions using `Stopwatch` for high-volume operations.

### Process & Concurrency Patterns
- **Process-Sequence**: Validates lifecycle hooks, multiple sequential operations, and fluent chaining.
- **Process-State**: Tests connection states, dispose behavior, and state machine transitions.
- **Process-Rule**: Validates input validation, service method discovery, and DI registration rules.
- **Signalled**: Orchestrates multithreaded send/receive flows to detect race conditions.
- **Deadlock-Resolution**: Verifies thread safety during concurrent encode/decode across types.
- **Loading-Test**: Gradual capacity increase tests with scaling message counts and payload sizes.
- **Resource-Stress-Test**: Memory pressure tests, rapid connect/disconnect, and allocation patterns.

## Test Suite Overview

| Project | Tests | Key Areas |
|---------|-------|-----------|
| `ProtobuffEncoder.Tests` | 268 | Core encode/decode, ValueSender/Receiver, attributes, collections, maps, oneof, inheritance, validation, streaming, schema generation, cross-file imports, service wiring, concurrency, stress |
| `ProtobuffEncoder.AspNetCore.Tests` | 43 | Input/output formatters, HttpClient extensions (TestHost), tiered setup validation, setup builder, options, strategy registration |
| `ProtobuffEncoder.Grpc.Tests` | 34 | Marshaller round-trips, service method discovery (all 4 types), channel extensions, DI registration |
| `ProtobuffEncoder.WebSockets.Tests` | 123 | WebSocket stream, retry policy, connection manager, client lifecycle, endpoint integration (TestHost) |
| `ProtobuffEncoder.Tool.Tests` | 12 | ProjectModifier csproj manipulation, duplicate prevention, subdirectory paths, batch operations |
| **Total** | **480+** | |

## Benchmarks

The benchmark suite (`benchmarks/ProtobuffEncoder.Benchmarks/`) measures performance across 7 categories:

| Benchmark | Description |
|-----------|-------------|
| `EncoderBenchmarks` | Core encode/decode for small and large messages |
| `CollectionBenchmarks` | Lists and dictionary/map encoding |
| `StaticMessageBenchmarks` | Pre-compiled vs. dynamic encode/decode comparison |
| `StreamingBenchmarks` | Delimited message write/read and sender/receiver round-trips |
| `ValidationBenchmarks` | Pipeline validation with valid and invalid messages |
| `SchemaGenerationBenchmarks` | .proto schema generation for various type complexities |
| `PayloadScalingBenchmarks` | Encoding at 100B, 10KB, and 100KB payload sizes |

Run benchmarks:
```bash
dotnet run -c Release --project benchmarks/ProtobuffEncoder.Benchmarks/
```

## How to Run Tests

Run all tests:
```bash
dotnet test
```

Run a specific project:
```bash
dotnet test tests/ProtobuffEncoder.Tests/
dotnet test tests/ProtobuffEncoder.AspNetCore.Tests/
dotnet test tests/ProtobuffEncoder.Grpc.Tests/
dotnet test tests/ProtobuffEncoder.WebSockets.Tests/
dotnet test tests/ProtobuffEncoder.Tool.Tests/
```

