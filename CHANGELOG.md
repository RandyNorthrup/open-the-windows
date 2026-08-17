# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/). Entries record what happened, not
what was planned (plans live in `PLAN.md`).

## [Unreleased]

### Added

- M2 (in progress): read-only detection engine core. `OpenTheWindows.Core.Abstractions`
  gains per-kind reader interfaces (registry, service, scheduled task, Appx,
  optional feature, Defender preference, power) plus interactive-user resolution,
  managed-setting detection and machine health probing, each with a snapshot
  record. `OpenTheWindows.Core.Engine.ScanEngine` compares each entry against the
  machine (per-kind compliance semantics, worst-of-actions aggregation,
  applicability short-circuit, interactive-user hive targeting) and produces a
  `ScanReport`. Consolidated `FakeReaders`/`FixedTimeProvider` test doubles and 28
  `ScanEngineTests`. First Windows reader: `WindowsRegistryReader` (64-bit view,
  `HKEY_USERS\<sid>` for the user hive — never `HKCU`; values materialised as JSON
  via `Utf8JsonWriter` so the trimmable project stays reflection-free), and
  `WindowsServiceReader` (start type incl. delayed auto-start from the registry,
  and run state, via `ServiceController`; new package
  `System.ServiceProcess.ServiceController` 10.0.11), and
  `WindowsScheduledTaskReader` (enabled state via the Task Scheduler 2.0 API;
  new package `TaskScheduler` 2.12.2; a missing task reads as `null`, which the
  engine treats as not-applicable), and `WindowsAppxReader` (per-user, all-users,
  provisioned and deprovisioned state via WinRT
  `Windows.Management.Deployment.PackageManager` plus the `Deprovisioned`
  registry key; the all-users / provisioned enumeration needs elevation and
  degrades to a documented user-scoped lower bound when denied, never a silent
  success), and `WindowsOptionalFeatureReader` (enabled state via WMI
  `Win32_OptionalFeature` — read-only and unelevated, chosen over the spec's
  DISM suggestion because a DISM online session needs elevation and can modify
  the image; deviation recorded in PLAN §7.1; new package `System.Management`
  10.0.11). All five readers have tests that read the real registry / SCM /
  task store / package store / WMI unelevated. Remaining Windows readers
  (Defender, power, interactive user, managed detection), report writers,
  `otw scan` / `otw health` and VM integration are still to come (see
  docs/milestones/M2-scan.md).

- M1: catalogue model (`OpenTheWindows.Core.Catalog`), JSON Schema
  `catalog/schema/tweak.schema.json`, embedded loader with directory overrides
  and a structural validator (stable rule ids), 29 Draft entries across six
  categories, and read-only CLI `otw catalog list|show|validate`. Full test
  suite (built-in catalogue, one negative per validator rule, loader/override,
  applicability, edition mapping, serialization round-trip, a 1000-entry
  performance guard, and CLI command tests) and a `catalog` quality gate that
  validates the embedded catalogue and is proven to reject
  `tests/fixtures/catalog-bad`.

- Executable milestone specifications `docs/milestones/M1..M8` with a runbook
  and certification template (`docs/milestones/README.md`); catalogue
  authoring guide `docs/catalog-format.md`.
- Enforcement: `build/start-milestone.ps1` (prints rules + spec, records an
  acknowledgement), project-local Claude Code hooks (`.claude/settings.json`,
  `build/hooks/*.ps1`) that block code edits without an acknowledgement, git
  pre-commit hook (`.githooks/pre-commit`, enabled by `build/setup-dev.ps1`),
  and the `plan` consistency gate (`build/check-plan.ps1`) in `quality.ps1`
  and CI.
- AGENTS.md: mandatory sequence and analyzer expectations learned in M0/M1.

### Changed

- PLAN.md section 8 is now an index/status board over the milestone specs.

### Fixed

- Flaky PSScriptAnalyzer check: `Invoke-ScriptAnalyzer` (PSScriptAnalyzer 1.25)
  intermittently threw a `NullReferenceException` from a compatibility rule,
  failing at random (this reddened an otherwise green `main`). Both the
  `powershell` quality gate (`build/quality.ps1`) and the CI PSScriptAnalyzer
  step now retry the analysis on a thrown exception (a tool crash, not a
  finding); real findings are still reported and never retried.
- Intermittent Release build failures (RT0002 on the generated WPF
  `*_wpftmp.csproj`, cascading to BG1002 "DesktopApp.baml cannot be found").
  ReferenceTrimmer analysed the transient markup-compilation project — which
  omits the code-behind that uses Core/Windows — and reported those project
  references as removable, which is an error under warnings-as-errors. The temp
  project is now excluded from ReferenceTrimmer (Directory.Build.props); it
  still runs on the real App project. Recorded in PLAN.md §7.3.
- CI publish failed with NU1004 (locked restore vs RID/trim publish graph). Every
  project now declares `RuntimeIdentifiers` (Directory.Build.props) and the
  Windows/CLI projects are `IsTrimmable`, so `packages.lock.json` matches both
  build and publish restores; lock files regenerated.

## [0.1.0] - 2026-08-16

Milestone M0: repository scaffold, quality gates, research.

### Added

- Solution `OpenTheWindows.slnx` with `OpenTheWindows.Core` (net10.0),
  `OpenTheWindows.Windows`, `OpenTheWindows.Cli` (`otw.exe`),
  `OpenTheWindows.App` (WPF), `OpenTheWindows.TestSupport` and four xunit.v3
  test projects (43 tests, 88% line / 83% branch coverage).
- Core domain model: `Category`, `Level`, `RiskTier`, `Scope`,
  `RestartRequirement`, `TweakId` (validated, no regex), `OperatingSystemFacts`,
  `IOperatingSystemInfo`, `IElevationContext`, `DoctorService`/`SystemReport`,
  `ExitCodes` (pinned contract), `AppInfo`.
- Windows adapters: `WindowsOperatingSystemInfo` (registry `CurrentVersion`,
  Windows 11 product-name correction, UBR), `WindowsElevationContext`.
- CLI: `otw doctor [--json]`, `--version`, `--help`; `asInvoker` manifest with
  long-path and UTF-8 code page.
- GUI: elevated WPF shell (`DesktopApp` composition root with validated DI
  graph, `MainWindow` with the system check, Fluent theme, UIA automation ids).
- Quality gates wired in `build/quality.ps1` and `.github/workflows/ci.yml`:
  restore (NuGetAudit as errors, locked mode in CI), format, build (analyzers
  at maximum, banned APIs, unused references, dead code), test, coverage
  thresholds, gitleaks, semgrep, jscpd, markdownlint, PSScriptAnalyzer. Each
  gate verified to fail on a broken input (PLAN.md §7.2).
- Repository configuration: `Directory.Build.props/targets`, central package
  management with pinned versions, `NuGet.config` with source mapping,
  `.editorconfig`, `BannedSymbols.txt`, `global.json` (SDK 10.0.400 + MTP),
  `.gitleaks.toml`, `.jscpd.json`, `.markdownlint-cli2.jsonc`,
  `PSScriptAnalyzerSettings.psd1`, `.config/dotnet-tools.json`, `package.json`.
- Scripts: `build/publish.ps1` (single-file trimmed CLI 13 MB, self-contained
  app, ZIP, CycloneDX SBOM, SHA256SUMS), `build/smoke-gui.ps1` (self-elevating
  window screenshot), `build/vm.ps1` (lab VM copy/run/doctor).
- Documentation: `README.md`, `PLAN.md`, `SECURITY.md`, `CONTRIBUTING.md`,
  ADRs 0001–0004, seven research reports under `docs/research/`
  (telemetry/privacy, landscape/enterprise requirements, Windows Update
  control, security hardening, performance/debloat, stack verification,
  enterprise safety architecture), agent instruction files (`AGENTS.md`,
  `CLAUDE.md`, `.cursor/rules`, `.github/copilot-instructions.md`), Dependabot.

### Changed

- Product tagline removed the same day it was proposed (product decision); the
  product ships with the title only.
- Windows Update pausing/holding declared a first-class feature (PLAN.md D21);
  the refusal list narrowed to permanent Disabled start types and
  WaaSMedicSvc/UsoSvc ownership hacks. README and agent instructions updated.

### Verified

- `otw doctor` exit 0 on the dev machine (Windows 11 Pro 22H2, not elevated)
  and on the lab VM (Windows 11 Pro 25H2 build 26200.9168, elevated).
- GUI launched elevated and rendered the system check (screenshot captured).

[Unreleased]: https://github.com/RandyNorthrup/open-the-windows/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/RandyNorthrup/open-the-windows/releases/tag/v0.1.0
