# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.7.0] - 2026-04-15

### Added

#### Core (`ProtobuffEncoder`)
- **`ProtobufDecodeException`** — typed decode-failure surface with `Offset` and `TargetType` context; replaces generic `InvalidOperationException` / `ArgumentOutOfRangeException` for all malformed-payload scenarios.
- **`ProtobufEncoder.TryDecode<T>(ReadOnlySpan<byte>, out T?, out string?)`** — non-throwing decode overload; returns `false` and sets an error string on malformed input, enabling monadic composition without try/catch.
- **`ContractResolver.ResolveLookup(Type)`** — `FrozenDictionary<int, FieldDescriptor>` per-type field-number lookup table built once alongside the existing ordered array; decode hot-paths now use O(1) dictionary lookup instead of a linear `Array.Find` scan.
- **`[ProtoEnum]` attribute** — controls how a CLR enum is rendered in the generated `.proto`: explicit prefix, `allow_alias`, `SuppressUnspecified`, and optional comment.
- **`[ProtoEnumValue]` attribute** — overrides the proto field number and/or rendered name for a single enum member; required when CLR values are non-sequential or two members must alias the same proto number.

#### Schema generation (`ProtobuffEncoder` / `ProtobuffEncoder.Tool`)
- **Proto3 best-practice enum generation** — enum values are now rendered in `SCREAMING_SNAKE_CASE` with an automatic `TYPE_NAME_` prefix; a zero-value `FOO_UNSPECIFIED = 0` sentinel is synthesised when no explicit zero entry exists (suppressible via `[ProtoEnum(SuppressUnspecified = true)]`); `option allow_alias = true` is emitted when `[ProtoEnum(AllowAlias = true)]` is set; `reserved` number and name blocks are supported.
- **Proto3 best-practice service generation** — each RPC now gets its own `FooRequest` / `FooResponse` wrapper message (proto3 best practice); `option deprecated = true` is emitted on deprecated services and individual RPCs (detected from `[Obsolete]`); `google.protobuf.Empty` is used for void return types with the `google/protobuf/empty.proto` import auto-added.
- **Well-known type mapping** — `DateTime` / `DateTimeOffset` → `google.protobuf.Timestamp`, `TimeSpan` → `google.protobuf.Duration`, `object` → `google.protobuf.Any`; required imports are collected and placed at the top of each file ahead of cross-file imports.
- **`ProtoEnumDef.AllowAlias`, `ReservedNumbers`, `ReservedNames`** — schema model now carries the metadata needed to render `option allow_alias`, `reserved N;`, and `reserved "name";` blocks.
- **`ProtoMessageDef.ReservedNumbers`, `ReservedNames`** — same reserved-field support for messages.
- **`ProtoRpcDef.IsDeprecated`, `Comment`** — per-RPC deprecated option and comment support.
- **`ProtoServiceDef.IsDeprecated`** — per-service deprecated option support.
- **`ProtoFile.WellKnownImports`** — tracks well-known type imports separately from cross-file imports so they are always sorted first.
- **`ProtoEnumValue.IsDeprecated`** — individual enum values can be marked deprecated (`[deprecated = true]`).

### Changed

#### Core (`ProtobuffEncoder`)
- **Decoder nesting depth cap** — `DecodeMessage` now enforces a maximum nesting depth of 512 (up from unlimited); payloads exceeding the cap throw `ProtobufDecodeException` with a clear message.
- **Varint reader** — `ReadVarintChecked` replaces the old unchecked `ReadVarint`; validates shift does not exceed 63 bits and throws `ProtobufDecodeException` on truncation or overflow rather than silently returning 0.
- **`SkipFieldChecked`** — unknown-field skip now validates bounds before advancing the offset; throws `ProtobufDecodeException` on truncated data or unknown wire types.
- **`ReadFixed32Value` / `ReadFixed64Value`** — boundary-checked slice before read; throw `ProtobufDecodeException` when fewer than 4/8 bytes remain.
- **`ReadLengthDelimitedValue`** — validates `length ≥ 0 && offset + length ≤ data.Length` before slicing; throws `ProtobufDecodeException` on out-of-bounds.
- **`WriteFixed32` / `WriteFixed64`** — replaced `BitConverter.GetBytes(value)` heap allocations with `BinaryPrimitives.Write*LittleEndian` into a `stackalloc byte[4/8]` span; zero heap allocation per scalar write.
- **`WriteLengthDelimited`** — `DateOnly`, `TimeOnly`, `Half`, `DateTimeOffset` encoding helpers rewritten to use `BinaryPrimitives` instead of `BitConverter.GetBytes`.
- **`ContractResolver.ResolveImplicit`** — now also populates `LookupCache` so implicitly-resolved nested types are accessible via `ResolveLookup` during decode.

#### Schema generation (`ProtobuffEncoder` / `ProtobuffEncoder.Tool`)
- **`ProtoSchemaGenerator`** — full rewrite of `BuildEnum`, `BuildService`, render methods, and CLR→proto type mapping to implement proto3 best practices; see Added section.
- **`ProtobufWriter.WriteDouble` / `WriteFloat` / `WriteFixed64`** — replaced `BitConverter.GetBytes` with `BinaryPrimitives` + `stackalloc`; zero heap allocation per write.

### Fixed

#### Core (`ProtobuffEncoder`)
- All malformed-input paths (`truncated varint`, `out-of-bounds slice`, `unknown wire type`) now throw the typed `ProtobufDecodeException` instead of `IndexOutOfRangeException` or `ArgumentOutOfRangeException`.

#### Schema generation
- `EnsureWrapper` de-duplicates RPC wrapper messages correctly when multiple RPCs on the same service share a request or response type.

## [1.7.0-prev] - 2026-04-15

#### Core (`ProtobuffEncoder`)
- **`ProtobufEncoder.EncodeTo(object, IBufferWriter<byte>)`** — new zero-copy encode overload that writes directly into a caller-supplied `IBufferWriter<byte>`, avoiding an intermediate heap array on the send path.

#### WebSockets (`ProtobuffEncoder.WebSockets`)
- **`ProtobufWebSocketConnection.SendDirectAsync`** — encodes into a pooled `ArrayBufferWriter<byte>` via `EncodeTo` and writes a single binary WebSocket frame; no `MemoryStream` allocation.
- **`ProtobufWebSocketConnection.ReceiveDirectAsync`** — reads a raw WebSocket frame into a pooled `ArrayPool<byte>` buffer and decodes it; no length-prefix overhead.
- **`ProtobufWebSocketConnection.ReceiveAllDirectAsync`** — async-enumerable companion to `ReceiveDirectAsync`.
- **`ProtobufWebSocketOptions.KeepAliveInterval`** — configures TCP keep-alive ping interval at the HTTP upgrade level via `WebSocketAcceptContext`.

#### gRPC (`ProtobuffEncoder.Grpc`)
- **`ProtobufGrpcServiceMethodProvider` binder cache** — open-generic binder methods are closed into typed delegates exactly once per unique service/method/type triple via a static `ConcurrentDictionary<BinderKey, Action<…>>`.  Repeated `OnServiceMethodDiscovery` calls (hot-reload, multi-registration) pay only a dictionary lookup.

#### Proto-gen tool (`ProtobuffEncoder.Tool`)
- **Auto-infer `[ProtoMethod]`** — `[ProtoService]` interfaces whose methods carry no `[ProtoMethod]` attribute now have all public methods included automatically; the gRPC method type is inferred from the signature (`IAsyncEnumerable<T>` in/out → streaming variants).
- **Top-level type file naming** — types in the global namespace (no `namespace` declaration) now generate per-type files (`<typename>.proto`) instead of all colliding into `default.proto`.
- **Tool README** — added `tools/ProtobuffEncoder.Tool/README.md` with installation, usage, configuration reference, file-naming table, and examples.

### Changed

#### WebSockets (`ProtobuffEncoder.WebSockets`)
- **`SendAsync`** marked `[Obsolete]` — prefer `SendDirectAsync`.
- **`ReceiveAsync`** marked `[Obsolete]` — prefer `ReceiveDirectAsync`.
- **`ReceiveAllAsync`** marked `[Obsolete]` — prefer `ReceiveAllDirectAsync`.
- **`WebSocketEndpointRouteBuilderExtensions`** — endpoint decorated with `.WithDisplayName` and `.DisableRequestTimeout`; `WebSocketAcceptContext.KeepAliveInterval` wired from options.
- **`WebSocketConnectionManager.BroadcastAsync`** — uses `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = Environment.ProcessorCount`.

#### gRPC (`ProtobuffEncoder.Grpc`)
- **`ProtobufMarshaller`** — switched to context-based `Marshaller<T>` constructor; serialisation writes into `SerializationContext.GetBufferWriter()`; deserialisation reads from `DeserializationContext.PayloadAsReadOnlySequence()`.

#### ASP.NET Core (`ProtobuffEncoder.AspNetCore`)
- **`ProtobufInputFormatter`** — uses `PipeReader.ReadToEndAsync` with zero-copy `FirstSpan` fast path.
- **`ProtobufOutputFormatter`** — writes via `Response.BodyWriter` (`PipeWriter`) with `ContentLength` pre-set.

#### Build
- **`Directory.Build.props`** — `TieredPGO=true` for all TFMs; `EnablePreviewFeatures=true` on net10; `LangVersion=latest` shared.

### Fixed

#### Proto-gen tool (`ProtobuffEncoder.Tool`)
- `ProtoSchemaGenerator.ResolveFileKey` — types with no namespace no longer all map to `default.proto`; each uses its lower-cased type name.
- `ProtoSchemaGenerator.ResolveServiceFileKey` — service interfaces with no namespace resolve by type name, not `"default"`.

## [1.6.0] - 2026-04-14

### Changed

#### ASP.NET Core (`ProtobuffEncoder.AspNetCore`)
- **`ProtobufInputFormatter`** — replaced `MemoryStream` body buffering with `PipeReader.ReadToEndAsync()`, draining the request body directly into a `ReadOnlySequence<byte>`.  For single-segment bodies (the common case) the payload is decoded from `buffer.FirstSpan` with zero extra allocations.  `reader.AdvanceTo(buffer.End)` is called in a `finally` block to correctly advance the pipe regardless of outcome.
- **`ProtobufOutputFormatter`** — response bytes are now written through `HttpContext.Response.BodyWriter` (`PipeWriter`) instead of `Response.Body`, allowing Kestrel to flush directly from its pipe memory.  `ContentLength` is set before writing so clients and proxies can pre-allocate receive buffers.

#### WebSockets (`ProtobuffEncoder.WebSockets`)
- **`WebSocketStream`** — replaced the `MemoryStream` receive buffer with an `ArrayPool<byte>.Shared` rental strategy.  A 64 KiB frame buffer is rented per-message and returned immediately after reassembly, so no heap memory is held between messages.  The message buffer grows via re-rent (copy + return old + rent 2× larger) for large frames.  Added `IAsyncDisposable` / `DisposeAsync()` for async graceful `NormalClosure` close, avoiding a blocking `GetAwaiter().GetResult()` on the thread-pool.  All public members carry full XML documentation.
- **`WebSocketConnectionManager`** — `BroadcastAsync` now uses `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = Environment.ProcessorCount` for concurrent fan-out without a LINQ `.Select` projection or `Task[]` allocation.  A point-in-time snapshot is taken before iteration so additions/removals during broadcast do not affect the current pass.  Failed sends are silently removed from the registry.  The `Connections` property uses a collection expression (`[.. _connections.Values]`) instead of `.ToList().AsReadOnly()`.
- **`WebSocketEndpointRouteBuilderExtensions`** — endpoint now accepts a `WebSocketAcceptContext` populated with `options.KeepAliveInterval`, configuring TCP keep-alive pings at the HTTP upgrade level.  The endpoint is decorated with `.WithDisplayName(...)` and `.DisableRequestTimeout()` so the ASP.NET Core request-timeout middleware does not kill idle long-lived connections.  Validation pipelines are built once per connection, not per message.
- **`ProtobufWebSocketOptions`** — added `KeepAliveInterval` property (defaults to `TimeSpan.Zero`; set to e.g. `TimeSpan.FromSeconds(30)` to detect silently dropped connections).

#### gRPC (`ProtobuffEncoder.Grpc`)
- **`ProtobufMarshaller`** — switched from the legacy `byte[]`-based `Marshaller<T>` constructor to the context-based overload (`Action<T, SerializationContext>` / `Func<DeserializationContext, T>`).  The serialisation path writes into the `IBufferWriter<byte>` supplied by gRPC via `SerializationContext.GetBufferWriter()`.  The deserialisation path reads from `DeserializationContext.PayloadAsReadOnlySequence()`, taking the `FirstSpan` fast path for single-segment (small message) buffers.
- **`ProtobufGrpcServiceMethodProvider`** — the open-generic binder methods (`BindUnary`, `BindServerStreaming`, etc.) are now closed into typed `Action` delegates exactly once per unique service/method/type combination using a static `ConcurrentDictionary` cache keyed on a `BinderKey` record struct.  Subsequent calls to `OnServiceMethodDiscovery` (hot-reload, multi-registration) pay only a dictionary lookup with no `MethodInfo.MakeGenericMethod` or `Delegate.CreateDelegate` allocation.  Unary and client-streaming handler lambdas were simplified from `async`/`await` wrappers to direct `Task<TResponse>` returns.

#### Build
- **`Directory.Build.props`** — `TieredPGO=true` applied per-TFM for net8, net9, net10.  `EnablePreviewFeatures=true` on net10 only.
- **`GenerateDocumentationFile`** — enabled on all `src/` library projects.

## [1.3.0] - 2026-03-24

### Added
- **Per-transport setup demos** — nine standalone projects (Simple, Normal, Advanced × REST, WebSockets, gRPC) under `demos/Setup/`.
- **Roslyn Analyser** (`ProtobuffEncoder.Analyzers`) with 10 compile-time diagnostics (PROTO001–PROTO010).
- **Templates** — three console app templates (Simple, Normal, Advanced) under `templates/`.
- Expanded test coverage for `ProtobufValueSender` and `ProtobufValueReceiver`.
- New integration tests for Tiered Setup Validation (Simple/Normal/Advanced).
- New `AddProtobufValidation` extension method in `ProtobuffEncoder.AspNetCore`.

### Changed
- Refined boilerplate structure: replaced single `Demo.Setup` with per-transport projects.
- Enabled multi-targeting for .NET 8, 9, and 10 across all test projects.
- Updated GitHub Actions CI/CD to use a build matrix for .NET 8, 9, and 10.
- Updated documentation and setup guides to reflect new demo structure.

## [1.2.0] - 2026-03-23

### Added
- New **Demo/Setup** documentation category with tiered examples (Simple, Normal, Advanced).
- Unified boilerplate project: `ProtobuffEncoder.Demo.Setup` demonstrating REST, WebSockets, and gRPC.

## [1.1.0] - 2026-03-23

### Added
- Comprehensive Benchmark suite covering 15 performance categories.
- Multi-runtime performance comparison across .NET 8, 9, and 10.
- Mermaid.js data visualization for performance metrics in documentation.
- Integrated JetBrains Writerside documentation for automated help authoring.

### Changed
- Refined `.gitignore` to exclude all build artifacts and benchmark results.

## [1.0.1] - 2026-03-20

### Fixed
- Fixed internal `ProtobufWriter` configuration for large nested messages.
- Corrected assembly scanning logic in `ContractResolver`.

### Added
- Pull Request and Bug Report templates.
- Contribution guidelines.

## [1.0.0] - 2026-03-15

### Added
- Initial release of **ProtobuffEncoder**.
- High-performance binary serialization engine.
- gRPC and WebSocket transport layers.
- ASP.NET Core MVC and HttpClient integration.
- `.proto` schema auto-generation.
