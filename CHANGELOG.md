# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/). Entries record what happened, not
what was planned (plans live in `PLAN.md`).

## [Unreleased]

### Added

- M3 (apply/verify/revert): the transactional change engine. `OpenTheWindows.Core.Engine.ApplyEngine`
  plans a run (dependency-ordered, flagging conflicts, managed settings, risk
  gating and not-applicable entries), then applies it journal-first — every prior
  state is written to the journal before the first machine change, each action is
  verified by re-reading, and any failure rolls back every already-applied action
  in reverse. `Revert` replays a prior run's captured state from the journal
  alone. `ActionApplier` executes all eight action kinds (capture / apply / verify
  / restore); code-level refusals (protected and protected-process services,
  disallowed executables) are enforced regardless of the catalogue. New writer
  interfaces in `Core.Abstractions` (registry, service, scheduled task, Appx,
  optional feature, Defender preference, power, command runner, restore point,
  Explorer restart, audit sink, pre-flight) and a hash-chained journal
  (`Core.Journal`, `docs/journal-format.md`). Value comparison is shared between
  scan and apply-verify (`StateComparison`). Windows adapters: `WindowsRegistryWriter`
  (policy values written through the Local GPO via COM `IGroupPolicyObject` and
  mirrored to the live hive so they survive `gpupdate`; preference and user-hive
  values written directly), `WindowsServiceWriter` (`ChangeServiceConfig`; refuses
  PPL and protected services — protected-process detection now populated in
  `WindowsServiceReader` via `QueryServiceConfig2`), `WindowsScheduledTaskWriter`,
  `WindowsAppxWriter`, `WindowsOptionalFeatureWriter` (drives the in-box `dism.exe`
  through the guarded command runner — deviation recorded in PLAN §7.1),
  `WindowsDefenderPreferenceWriter`, `WindowsPowerSettingWriter`, `WindowsCommandRunner`
  (the single sanctioned `Process` host), `WindowsJournalStore` (ProgramData with a
  restrictive ACL and hash chain), `WindowsRestorePointService` (`SRSetRestorePointW`,
  truthful Created/Skipped24h/Disabled), `WindowsAuditSink` (Windows Event Log +
  monthly JSONL, `docs/event-log.md`), `WindowsPreflight` and `WindowsExplorerRestarter`.
  New CLI: `otw apply`, `otw revert`, `otw history`. New package
  `System.Diagnostics.EventLog` 10.0.11. Coverage thresholds raised to 90 line /
  80 branch, with the OS-mutating writer/interop namespaces scoped out of coverage
  (verified by the `OTW_INTEGRATION` VM tests, not units).
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
  10.0.11), and `WindowsDefenderPreferenceReader` (a named `MSFT_MpPreference`
  property read from WMI and materialised to JSON with `Utf8JsonWriter`;
  Defender absent or property unknown ⇒ `null`). The trim-safe JSON
  materialisation helper is now shared (`JsonMaterialization`) between the
  registry and Defender readers. `WindowsPowerSettingReader` reads a setting's
  AC/DC value indices for the active scheme via `powrprof.dll`
  (`PowerGetActiveScheme` / `PowerRead{AC,DC}ValueIndex`), declared with the
  `[LibraryImport]` source generator (chosen over the spec's CsWin32 — no
  build-time generator dependency; deviation recorded in PLAN §7.1); every
  P/Invoke pins `DllImportSearchPath.System32` (CA5392) and `AllowUnsafeBlocks`
  is enabled for the Windows project only, for the generated marshalling stubs.
  `WindowsInteractiveUserResolver` resolves the interactive (console/RDP)
  session's signed-in user via the Terminal Services API
  (`WTSQuerySessionInformation`) — never the elevated process token — and maps
  the account to a SID with the managed `NTAccount` translation, reporting
  whether `HKEY_USERS\<sid>` is loaded. All eight readers have tests that read
  the real registry / SCM / task store / package store / WMI / power scheme /
  session unelevated. `WindowsManagedSettingDetector` reports whether a registry
  value is owned by the Local Group Policy by reading the `Registry.pol` files
  through a new read-only PReg parser (`OpenTheWindows.Core.Policy.
  PolicyRegistryFile` + `PolicyRegistryEntry`) — chosen over an RSOP query
  because the `.pol` files are world-readable (no elevation) and deterministic;
  per-value MDM attribution and per-user MLGPO are deferred to the policy engine
  (M5), and a value that cannot be positively tied to a policy is reported as
  not-managed (the safe direction — it never hides real drift). The PReg parser
  has golden-fixture tests and the detector has deterministic tests over a
  temporary policy root (new shared `PRegFixture` test helper). This completes
  every read-only state reader for the M2 detection engine. Remaining M2 work
  (28-check machine health probe, JSON/CSV/HTML/SARIF report writers, `otw scan`
  / `otw health`, the no-write architectural guard, Stryker.NET, and VM
  integration) is still to come (see docs/milestones/M2-scan.md).

- M2: scan report writers (`OpenTheWindows.Core.Reports`). `IReportWriter` with
  four formats — `JsonReportWriter` (source-generated, camelCase, indented),
  `CsvReportWriter` (RFC 4180, `\n` line endings, one row per action),
  `HtmlReportWriter` (a single self-contained file: inline CSS, no external
  scripts/styles/fonts/images, so it renders offline as archivable evidence),
  and `SarifReportWriter` (SARIF 2.1.0; one rule per drifting id; result `level`
  derived from the entry's risk tier — Breaking ⇒ error, Advanced ⇒ warning,
  else note). `TweakObservation` now carries the entry's `RiskTier` so reports
  can express severity. Golden/validation tests cover all four (CSV exact-string
  golden, JSON/SARIF parse-and-assert, HTML self-containment).

- M2: read-only machine health probe (`WindowsMachineHealthProbe`,
  `IMachineHealthProbe`). The 28 checks from research 04 §5 — Secure Boot, TPM,
  Defender real-time/tamper, BitLocker, VBS/HVCI/Credential Guard, LSA PPL,
  kernel DMA, build support, local admins, join state, firewall, RDP, SMB,
  remote-management services, sudo/Developer Mode, Smart App Control,
  vulnerable-driver blocklist, core isolation, edition, Windows RE, host/
  port-proxy exposure, event-log size, Defender exclusions, local security
  policy, third-party AV and MDM enrolment — each reading only (registry,
  WMI, services). A check whose source needs elevation reports `Unknown` with an
  explanation rather than failing or inventing a value; every check is wrapped so
  the probe never throws. Tested against the real machine unelevated (28 unique
  checks, all with detail; the registry-backed checks return a real verdict).

- M2: `otw scan` and `otw health` CLI commands. `otw scan --profile
  <basic|balanced|strict|paranoid>` runs a read-only drift scan of a built-in
  level profile (`NamedProfile` selects Verified entries at or below the level;
  `--include-draft` adds drafts; `--catalog-dir` overrides) and prints a text
  summary or, with `--json`/`--csv`/`--html`/`--sarif` (and optional `--out`),
  a report; exit 0 compliant, 1 drift, 2 error, 3 unsupported platform.
  `otw health [--json]` runs the 28 read-only checks; exit 0. The CLI composes
  the real Windows readers into the `ScanEngine` and the health probe; command
  tests drive both through the parser with fake readers/probe (drift lists ids,
  unsupported exits 3, unknown profile and conflicting formats exit 2, report
  formats emit to stdout/file). New docs `docs/reports.md`; README documents
  both commands.

- M2: read-only architectural guard. A test (`ReadOnlyArchitectureTests`)
  metadata-scans the compiled Windows assembly's member references and fails if
  any mutating registry / service / package / task / file API is referenced
  (`RegistryKey.SetValue`/`Delete*`, `ServiceController.Start/Stop`,
  `PackageManager.Add/Remove/Stage/…`, task `Register`/`Delete`, `File`/
  `Directory` writes). It matches the declaring type and member, so ordinary
  `List.Add` and friends are not flagged. Proven to fail on an injected
  `SetValue` and pass once reverted (PLAN §7.2).

- M2: mutation-testing step wired (Stryker.NET 4.16.0, pinned in
  `.config/dotnet-tools.json` with `stryker-config.json` scoped to the M2 Core
  logic) as the opt-in `pwsh build/quality.ps1 -Only mutation` gate (report-only:
  `break=0`; never part of `all`; real threshold from M3). It is currently a
  **deferred gate**: Stryker does not yet support Microsoft.Testing.Platform test
  projects (stryker-net#3094) and this repo mandates MTP (no VSTest), so the gate
  detects that condition and reports DEFERRED rather than a spurious failure —
  any other Stryker error still fails it. Recorded in PLAN §7.1.

- M2: VM integration tests (`VmIntegrationTests`, gated by `OTW_INTEGRATION=1`
  via `Assert.SkipUnless`, skipped in normal runs): registry `CurrentBuild`
  matches the OS, the Print Spooler service and the built-in ScheduledDefrag task
  read back, interactive-user resolution returns the session user's SID, managed
  detection is NotManaged on an unmanaged machine, and the health probe returns
  all 28 checks. Verified end-to-end on the lab VM (Windows 11 Pro 25H2,
  build 26200.9168, elevated): `otw scan --profile balanced --include-draft`
  exits 1 and lists the drifting ids; setting a tweak's two policy values flips
  that entry to Compliant; `otw health` prints 28 checks with real elevated
  values (e.g. TPM and BitLocker return verdicts, not Unknown); and all four
  report formats write to file. The VM was reverted to its prior state
  afterwards.

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
- Milestone M2 (Scan) certified and marked DONE: `docs/certification/M2.md`
  records all 12 gates green (line 89.3% / branch 75.3%) and the five acceptance
  criteria verified end-to-end on the lab VM (Windows 11 Pro 25H2). PLAN.md
  section 8 and `docs/milestones/M2-scan.md` updated; the M1 status-board row was
  also corrected to DONE.

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
