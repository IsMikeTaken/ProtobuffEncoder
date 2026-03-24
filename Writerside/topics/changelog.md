# Changelog

All notable changes to this project will be documented in this file.

## [1.3.0] - 2026-03-24

### Added {id="v130_added"}
- **Per-transport setup demos** — nine standalone projects (Simple, Normal, Advanced × REST, WebSockets, gRPC) each with their own `Program.cs` under `demos/Setup/`.
- Shared models project (`ProtobuffEncoder.Demo.Setup.Shared`) with common contracts and gRPC service interfaces.
- **Roslyn Analyser** (`ProtobuffEncoder.Analyzers`) with 10 compile-time diagnostics (PROTO001–PROTO010) for missing fields, duplicate numbers, invalid ranges, and more.
- **Templates** — three console app templates (Simple, Normal, Advanced) under `templates/` demonstrating core encode/decode, collections, auto-discovery, and schema generation.
- Advanced demos print resolver output: field numbering strategies, generated `.proto` schemas, registration status, and polymorphism round-trips.

### Changed {id="v130_changed"}
- Replaced the single `ProtobuffEncoder.Demo.Setup` project with nine per-transport projects.
- Restructured the "Demo" section in the documentation table of contents; setup guides now live under **Demos > Setup Guides**.
- Updated simple, normal, and advanced setup documentation to reflect the new per-transport structure with accurate API usage and expected console output.
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

