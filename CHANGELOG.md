# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
