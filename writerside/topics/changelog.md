# Changelog

All notable changes to this project will be documented in this file.

## [1.4.0] - 2026-03-25

### Added {id="v140_added"}
- **5 new Roslyn analyser diagnostics** (PROTO011–PROTO015) bringing the total to 15 compile-time checks:
  - `ProtoServiceAnalyzer`: PROTO011 (service with no methods), PROTO012 (streaming return type mismatch).
  - `ProtoIncludeAnalyzer`: PROTO013 (include/field number conflict), PROTO014 (include type not derived), PROTO015 (ProtoMap on non-Dictionary).
- **Comprehensive analyser test suite** — 38 tests covering all 15 diagnostics (PROTO001–PROTO015) with both positive and negative cases.
- **Analyser reference documentation** — new `analyzers_reference.md` page documenting all 15 diagnostics with examples, severity, and fix guidance.
- **GitHub Actions CI/CD workflows** — three new workflows (`ci-pr.yml`, `ci-development.yml`, `ci-release.yml`) for source projects with incremental versioning and publishing to GitHub Packages.
  - PR builds: `1.3.0-pr.<number>.<run>` preview packages.
  - Development builds: `1.3.0-dev.<run>` pre-release packages pushed to GitHub Packages.
  - Master builds: stable `1.3.0` packages, automatic Git tag and GitHub Release with attached `.nupkg` files.
- `Directory.Build.props` for centralised version management and NuGet package metadata.
- Each CI workflow tests against .NET 8, 9, and 10 in parallel with framework-specific `--framework` filtering to ensure no tests are silently skipped.
- Post-test verification step that fails the build if no `.trx` result files are produced.

### Changed {id="v140_changed"}
- **Templates restructured** — all three console templates (Simple, Normal, Advanced) split into `Contracts/`, `Services/`, and `Program.cs` for clean separation. Each showcases at least one `[ProtoService]` interface and two `[ProtoContract]` types. Removed ASCII-art comment blocks in favour of natural guiding comments.
  - Simple: `WeatherRequest`, `WeatherForecast`, `DayEntry` contracts + `IWeatherService` (Unary, ServerStreaming).
  - Normal: `Team`, `SensorReading`, `Alert`, `ChatMessage`, `ChatReply` contracts + `IChatService` (Unary, DuplexStreaming).
  - Advanced: `Customer`, `Invoice`, `Product`, `InventoryQuery`, `StockLevel`, `AttributedProduct` contracts + `IInventoryService` (Unary, ServerStreaming).
- Updated simple, normal, and advanced setup documentation to match new template content with expected console output.
- Analyzer test project (`ProtobuffEncoder.Analyzers.Tests`) retargeted from `netstandard2.0` to `net8.0` so tests actually execute. Added explicit `Microsoft.CodeAnalysis.CSharp` reference to resolve version conflict with analyzer testing package.

### Fixed {id="v140_fixed"}
- CI `--framework` filter now targets a specific runtime per matrix entry instead of installing a single SDK and hoping the other frameworks build. All three SDKs are installed in every job.

## [1.3.0] - 2026-03-24

### Added {id="v130_added"}
- **Per-transport setup demos** — nine standalone projects (Simple, Normal, Advanced x REST, WebSockets, gRPC) each with their own `Program.cs` under `demos/Setup/`.
- Shared models project (`ProtobuffEncoder.Demo.Setup.Shared`) with common contracts and gRPC service interfaces.
- **Roslyn Analyser** (`ProtobuffEncoder.Analyzers`) with 10 compile-time diagnostics (PROTO001-PROTO010) for missing fields, duplicate numbers, invalid ranges, and more.
- **Templates** — three console app templates (Simple, Normal, Advanced) under `templates/` demonstrating core encode/decode, collections, auto-discovery, and schema generation.
- Expanded test coverage for `ProtobufValueSender` and `ProtobufValueReceiver`.
- New integration tests for Tiered Setup Validation (Simple/Normal/Advanced).
- New `AddProtobufValidation` extension method in `ProtobuffEncoder.AspNetCore`.
- Advanced demos print resolver output: field numbering strategies, generated `.proto` schemas, registration status, and polymorphism round-trips.

### Changed {id="v130_changed"}
- Replaced the single `ProtobuffEncoder.Demo.Setup` project with nine per-transport projects.
- Restructured the "Demo" section in the documentation table of contents; setup guides now live under **Demos > Setup Guides**.
- Updated simple, normal, and advanced setup documentation to reflect the new per-transport structure with accurate API usage and expected console output.
- Enabled multi-targeting for .NET 8, 9, and 10 across all test projects.
- Updated GitHub Actions CI/CD to use a build matrix for .NET 8, 9, and 10.
- Overhauled demos overview page with the new folder structure and a setup guide summary table.

### Fixed {id="v130_fixed"}
- WriterSide `CDE016` errors: replaced 224 `cs` code fences with `C#`.
- WriterSide `INT009` errors: replaced unsupported `xychart-beta` Mermaid diagrams with `pie` charts.
- Corrected Roslyn analyser `RS1032` warning on PROTO004 diagnostic message formatting.

## [1.2.0] - 2026-03-23

### Added {id="v120_added"}
- New **Demo/Setup** documentation category with tiered examples (Simple, Normal, Advanced).
- Unified boilerplate project: `ProtobuffEncoder.Demo.Setup` demonstrating REST, WebSockets, and gRPC.

## [1.1.0] - 2026-03-23

### Added {id="v110_added"}
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

### Added {id="v101_added"}
- Pull Request and Bug Report templates.
- Contribution guidelines.

## [1.0.0] - 2026-03-15

### Added {id="v100_added"}
- Initial release of **ProtobuffEncoder**.
- High-performance binary serialization engine.
- gRPC and WebSocket transport layers.
- ASP.NET Core MVC and HttpClient integration.
- `.proto` schema auto-generation.

