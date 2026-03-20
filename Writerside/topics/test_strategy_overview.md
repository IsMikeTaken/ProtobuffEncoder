# Test Strategy

## Overview

ProtobuffEncoder maintains **430+ tests** across 5 test projects, using FIRST-U Pass/Fail testing patterns for systematic coverage of all framework functionality.

## FIRST-U Testing Patterns

| Pattern | Description | Example |
|---------|-------------|---------|
| **Simple-Test** | Basic functionality verification | Encode a single field, verify bytes |
| **Code-Path** | Exercise specific code branches | Nullable field, empty collection, enum |
| **Boundary** | Edge cases and limits | Max int, empty string, zero-length array |
| **Negative** | Invalid input handling | Null arguments, missing fields, bad wire data |
| **Rollback** | Recovery after failure | Encode after failed decode, receiver after error |
| **Service-Simulation** | Validation as boundary | Pipeline rejection, validated sender/receiver |
| **Resource-Stress** | High volume scenarios | Many large messages, duplex high throughput |
| **Loading-Test** | Scaling behavior | Parameterized message count/payload size |
| **Deadlock-Resolution** | Concurrent safety | Parallel encode/decode, concurrent stream ops |
| **Bit-Error-Simulation** | Corrupted data tolerance | All-zero bytes, random byte payloads |
| **Process-State** | Stateful component testing | StaticMessage reuse, concurrent StaticMessage |
| **Component-Simulation** | Full pipeline integration | Encode -> stream -> decode end-to-end |

## Test Projects

### ProtobuffEncoder.Tests (200+ tests)

Core library tests covering:

| Test File | Tests | Coverage |
|-----------|-------|----------|
| `ProtobufEncoderTests.cs` | 31 | All scalar types, encode/decode round-trip |
| `CollectionTests.cs` | 13 | Lists, arrays, packed/unpacked encoding |
| `MapFieldTests.cs` | 7 | Dictionary serialization, nested map values |
| `OneOfTests.cs` | 5 | OneOf encoding, only-first semantics |
| `InheritanceTests.cs` | 6 | ProtoInclude, base/derived field handling |
| `StreamingTests.cs` | 12 | Delimited messages, async streams |
| `StaticMessageAndTransportTests.cs` | 18 | StaticMessage, Sender, Receiver, DuplexStream |
| `ValidationTests.cs` | 25 | Pipeline, behaviors, validated transport |
| `ContractResolverTests.cs` | 26 | Field resolution, auto-numbering, explicit fields |
| `AttributeFlexibilityTests.cs` | 20 | All attribute combinations |
| `ServiceGeneratorTests.cs` | 33 | Schema generation, services, imports, versioning |
| `CommonTypesTests.cs` | 3 | Guid, DateTime, TimeSpan |
| `AdvancedPatternTests.cs` | 19 | Rollback, stress, deadlock, bit-error patterns |
| `InsanityTests.cs` | 5 | Extreme edge cases |
| `ExtremeBreakerTests.cs` | 3 | Boundary-breaking scenarios |

### ProtobuffEncoder.AspNetCore.Tests (41 tests)

| Test File | Tests | Coverage |
|-----------|-------|----------|
| `ProtobufInputFormatterTests.cs` | 8 | Media type, CanReadType, decode, empty body, large payloads |
| `ProtobufOutputFormatterTests.cs` | 7 | Write, null object, content-length, empty contract |
| `ProtobufHttpContentTests.cs` | 6 | Headers, serialization, null guard, length |
| `HttpClientIntegrationTests.cs` | 5 | POST round-trip, fire-and-forget, GET, sequential, concurrent |
| `SetupTests.cs` | 15 | Options defaults, Builder API, strategies, DI, formatters |

### ProtobuffEncoder.Grpc.Tests (34 tests)

| Test File | Tests | Coverage |
|-----------|-------|----------|
| `ProtobufMarshallerTests.cs` | 10 | Creation, round-trip, edge cases, performance |
| `ServiceMethodDescriptorTests.cs` | 14 | All 4 method types, interface-only, edge cases |
| `GrpcExtensionsTests.cs` | 10 | Client validation, DI registration, duplicates |

### ProtobuffEncoder.WebSockets.Tests (117 tests)

| Test File | Tests | Coverage |
|-----------|-------|----------|
| `WebSocketStreamTests.cs` | 25 | Read/write, framing, close, binary mode |
| `ProtobufWebSocketConnectionTests.cs` | 18 | Send, receive, lifecycle, metadata |
| `WebSocketConnectionManagerTests.cs` | 19 | Add, remove, broadcast, filtered, concurrent |
| `ProtobufWebSocketClientTests.cs` | 17 | Connect, send, receive, retry, lifecycle |
| `ProtobufWebSocketOptionsTests.cs` | 17 | All option properties, defaults, callbacks |
| `RetryPolicyTests.cs` | 13 | Delay calculation, backoff, overflow, presets |
| `EndpointIntegrationTests.cs` | 8 | Full server-client round-trip |

### ProtobuffEncoder.Tool.Tests (12 tests)

| Test File | Tests | Coverage |
|-----------|-------|----------|
| `ProjectModifierTests.cs` | 12 | Append, dedup, ItemGroup create/reuse, batch |

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific project
dotnet test tests/ProtobuffEncoder.Tests

# Run with filter
dotnet test --filter "FullyQualifiedName~Validation"

# Run with detailed output
dotnet test -v detailed
```
