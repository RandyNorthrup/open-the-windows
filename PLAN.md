# Open the Windows — Plan

Living plan: assumptions, decisions, architecture, research, milestones and the
certification gates each milestone must pass. Update it when a decision changes;
record what actually happened in `CHANGELOG.md`.

Last updated: 2026-08-16 (M0 complete).

---

## 1. Product

**Open the Windows** is a free, MIT-licensed, enterprise-grade Windows 11
utility (GUI + CLI) that lets a user or an administrator:

- turn off telemetry / diagnostic data / tracking features, selectively;
- pause, defer, target and otherwise control Windows Update safely;
- apply security-hardening settings mapped to Microsoft's baseline, DISA STIG
  and CIS section IDs;
- apply measurable performance tweaks and remove inbox bloat;
- do all of the above at chosen **levels** per category, with per-tweak **risk
  badges**, full **reversibility**, **drift detection** and **remediation**,
  and an **audit trail** — without fighting an organisation's MDM/GPO.

Principles (non-negotiable):

1. **Provably safe.** Every tweak is data with sources, a revert path, a risk
   tier and the builds it was verified on. Nothing is applied that is not
   journaled first. Nothing "placebo" ships (see the snake-oil list in
   `docs/research/05-performance-debloat-catalog.md` §7).
2. **Never disable Windows Update, Defender, UAC, or protected services.**
   Refusals are explicit and documented (research 03 §"refuse", 05 §7).
3. **Respect management.** Managed (MDM/GPO) settings are shown as managed and
   not overridden without a documented break-glass flag.
4. **Zero telemetry of our own. Offline first.** No network access unless the
   user turns on the optional update check.
5. **One engine, two faces.** Core + Windows adapters; the CLI and the WPF GUI
   are thin.

## 2. Branding and licensing decisions

| Item | Decision | Source |
| ------ | ---------- | -------- |
| Title | **Open the Windows** | product owner, 2026-08-16 |
| Tagline | **None.** (A tagline was proposed and then withdrawn the same day.) | product owner, 2026-08-16 |
| CLI executable | `otw.exe` | this plan |
| Price | Free. No paid tier, no "premium" features. | product owner, 2026-08-16 |
| License | MIT (ADR 0002) | product owner |
| Code signing | None (ADR 0003). Compensating: reproducible builds, SHA-256, SBOM, GitHub attestations. | product owner |
| Repository | `https://github.com/RandyNorthrup/open-the-windows` (assumed from the GitHub account in use; not yet pushed) | assumption |

## 3. Assumptions and resolved decisions

| # | Decision | Status | Rationale / source |
| --- | ---------- | -------- | -------------------- |
| D1 | .NET 10 LTS (SDK 10.0.400 pinned in `global.json`) | Resolved | research 06; LTS to 2028-11-14 |
| D2 | WPF (.NET 10 Fluent `ThemeMode`) for the GUI | Resolved by owner | ADR 0001 |
| D3 | Layering: Core (net10.0) / Windows (net10.0-windows10.0.26100.0) / Cli / App + TestSupport + 4 test projects | Resolved | ADR 0001 |
| D4 | Tweaks are JSON data validated by schema; actions are a closed set of typed kinds | Resolved | ADR 0004 |
| D5 | Distribution: portable ZIP + MSI (WiX 7) + winget manifest; no MSIX | Resolved by owner | ADR 0003; WiX 7.0.0 is MS-RL (tool only, nothing linked) |
| D6 | Minimum OS: Windows 11 21H2 (build 22000); Server SKUs refused; x64 and ARM64 | Resolved | `OperatingSystemFacts.FirstWindows11Build`; ARM64 built by CI, smoke deferred (no ARM64 device) |
| D7 | Editions: Home, Pro, Enterprise, Education all first-class; catalogue rows carry `appliesTo.editions` because many policies are Pro+/Ent-only | Resolved | research 01, 03 |
| D8 | Test runner: xunit.v3 4.0 on Microsoft.Testing.Platform; `global.json` opts `dotnet test` in; no VSTest packages | Resolved | .NET 10 SDK refuses VSTest mode for MTP projects (observed) |
| D9 | Coverage: `Microsoft.Testing.Extensions.CodeCoverage` → Cobertura → ReportGenerator thresholds (line ≥ 85, branch ≥ 75; raise at M3 to 90/80) | Resolved | thresholds verified to fail (see §7) |
| D10 | Assertions: plain xUnit `Assert`; mocks: hand-written fakes in `OpenTheWindows.TestSupport` (NSubstitute only if a real need appears) | Resolved | fewer deps; FluentAssertions 8 licence issue avoided |
| D11 | Profile signing: detached **ES256** (ECDSA P-256) — .NET 10 has no standalone Ed25519 | Resolved | research 07 |
| D12 | Policy writes go through the Local GPO (`IGroupPolicyObject` → `Registry.pol`) **and** are mirrored to the registry; raw registry only where GP is absent (Home) | Resolved | research 07 §1 |
| D13 | Per-user writes target the *interactive session user's* `HKU\<SID>` (resolved via WTS APIs), never the elevating account's HKCU (`Registry.CurrentUser` is a banned API in `src/`) | Resolved | research 07 §9; `BannedSymbols.txt` |
| D14 | CLI runs `asInvoker`, exits 5 when elevation is needed; GUI runs `requireAdministrator` | Resolved | research 07 §9 |
| D15 | Exit-code contract pinned in `ExitCodes` (0 ok / 1 drift / 2 error / 3 unsupported / 4 invalid input / 5 elevation / 3010 reboot) | Resolved | Intune remediation compatibility |
| D16 | Time comes from `TimeProvider` (DateTime.Now/UtcNow banned in `src/`) | Resolved | testable journals |
| D17 | Lab VM: Windows 11 Pro 25H2 (26200.9168) on VMware, SSH key auth; connection via env vars (`.env.example`) | Resolved | owner-provided |
| D18 | Localisation: en-US strings now; resx infrastructure at M6 | Assumption | cheap to reverse |
| D19 | ReportGenerator threshold syntax is the bare `minimumCoverageThresholds:...` token | Resolved | `-settings:` forms silently ignored (verified) |
| D20 | Coverage collector: `Microsoft.Testing.Extensions.CodeCoverage`, not coverlet | Resolved | coverlet hooks VSTest targets |

## 4. Open questions

| # | Question | Impact | Owner |
| --- | ---------- | -------- | ------- |
| Q1 | VMware host type for the lab VM (Workstation → `vmrun`; ESXi/vSphere → `govc`) so certification runs can revert to a clean checkpoint automatically. Until answered, M3+ integration runs rely on in-guest System Restore + the app's own journal. | M3 certification automation | owner |
| Q2 | Should "enterprise mode" hide consumer-only UI (e.g. Gamer profile) — default: no, profiles are just data. | M5 | owner |
| Q3 | Publish framework-dependent builds too (small download, needs .NET 10 Desktop Runtime) in addition to self-contained? Recommendation: yes for the MSI, self-contained for the portable ZIP. | M7 | owner |
| Q4 | Winget publication account / manifest PR ownership. | M7 | owner |
| Q5 | Do we want an optional Sysmon install helper (adjacent, out of core scope)? | M4 backlog | owner |

## 5. Architecture

### 5.1 Projects

| Project | TFM | Role |
| --------- | ----- | ------ |
| `OpenTheWindows.Core` | net10.0 | Domain model (`Category`, `Level`, `RiskTier`, `Scope`, `RestartRequirement`, `TweakId`), abstractions (`IOperatingSystemInfo`, `IElevationContext`, later `IRegistry`, `IServiceControl`, `ITaskScheduler`, `IPackageManager`, `IPolicyWriter`, `IJournal` …), catalogue loader + schema validation, engine (plan/apply/verify/revert/scan), journal, reports, `DoctorService`, `ExitCodes`, `AppInfo`. No Windows references. Trimmable, AOT-compatible. |
| `OpenTheWindows.Windows` | net10.0-windows10.0.26100.0 (min 22000) | The only OS-touching project: `WindowsOperatingSystemInfo`, `WindowsElevationContext`, later registry/service/task/Appx/DISM/WMI/WUApi/GPO adapters. |
| `OpenTheWindows.Cli` | same | `otw.exe` (System.CommandLine 2.0.11). Commands today: `doctor [--json]`, `--version`, `--help`. |
| `OpenTheWindows.App` | same, WPF | `OpenTheWindows.exe`; DI composition root `DesktopApp`; MVVM (CommunityToolkit.Mvvm); Fluent theme. |
| `tests/*` | | `TestSupport` (fakes), `Core.Tests`, `Windows.Tests` (read-only real-OS), `Cli.Tests` (in-process invocation with captured output), `App.Tests` (view models + DI graph). |

### 5.2 Catalogue entry (schema to be authored at M1)

`id, title, description, rationale, category, level, risk, scope, appliesTo
{editions[], minBuild, maxBuild, architectures[]}, actions[], detect,
revert (explicit when not derivable), requires (None/ExplorerRestart/SignOut/
Reboot), dependsOn[], conflictsWith[], managedBy (policy CSP / GPO path for
"managed" detection), sources[] (≥1 URL), verifiedOn[] {build, date, evidence},
tags[]`.

Action kinds (closed set, each with detect/apply/revert semantics): `registry`
(preference or policy; policy kind goes through GPO + mirror), `service`
(start type, never protected services), `scheduledTask` (enable/disable),
`appx` (remove for user / all users / deprovision, with `Deprovisioned` marker),
`optionalFeature`/`capability`, `defenderPreference` (WMI `MSFT_MpPreference`),
`powerSetting`, `firewall`, `auditPolicy`, `localSecurityPolicy`,
`explorerRestart` (side effect), and a guarded `command` kind (allow-listed
executables only, journaled, no shell).

### 5.3 Engine contract

`plan(profile) → diff` (what-if, no writes) → `apply(diff)`:
pre-flight (elevated, supported OS, not Server, no pending reboot for reboot-
requiring tweaks, no servicing operation in progress, not OOBE), optional System
Restore point (verified created; the 24-hour skip is disclosed), **journal
first** (prior state incl. "absent"), apply in dependency order, verify after
write, roll back in reverse on failure, aggregate `RestartRequirement`, write
Event Log + JSONL audit records. `scan(profile)` reports drift and exits 1;
`remediate` = idempotent apply. Managed settings are reported, not overridden,
unless `--break-glass` (still journaled, with a warning that policy refresh will
revert).

### 5.4 Levels, risk and profiles (from research 02)

- Per-category dial: `Off / Basic / Balanced / Strict / Paranoid`
  (`Core.Model.Level`).
- Per-tweak risk badge: `Safe / Caution / Advanced / Breaking`
  (`Core.Model.RiskTier`). Built-in profiles never include `Breaking`;
  `Breaking` requires typed confirmation; unattended mode requires an explicit
  allow-list for `Advanced`/`Breaking`.
- Named profiles are saved dial positions + overrides: Home, Power User,
  Enterprise Workstation, Developer, Gamer, Kiosk/Shared, Privacy-Max, Custom.
- "Revert my changes" (journal replay) is a different action from "Reset to
  Windows defaults" (catalogue defaults).

### 5.5 Safety rails (research 07 §7, research 02 §4)

Interactive-user hive resolution; GPO-aware policy writes; MDM/GPO managed
detection (`PolicyManager`, RSOP, GP history); never touch WaaSMedicSvc,
wuauserv, UsoSvc, DoSvc, BITS, TrustedInstaller, SecurityHealthService,
WinDefend, wscsvc, EventLog and other protected/PPL services; no hosts-file or
firewall blocking of Microsoft endpoints; Windows Update pause capped at 35 days
by default (extension only behind a warning and a hard ceiling); Defender
signature updates never blocked; Edge/WebView2 never removed; OneDrive removal
warns about Known Folder Move; feature-update re-apply via a scheduled task that
compares build/UBR; Explorer restart implemented for the correct session.

## 6. Research digest (all reports under `docs/research/`)

| Report | What it holds | Size |
| -------- | --------------- | ------ |
| `01-telemetry-privacy-catalog.md` | 248 privacy/telemetry control rows (policy vs preference, scope, editions, revert, side effects, level), 24 tasks, 21 services, 22 dangerous practices, MDM/GPO section, RTLFB summary, endpoint reference; 72 sources | ~142 KB |
| `02-landscape-and-enterprise-requirements.md` | Feature matrix of ~25 prior-art tools, breakage catalogue B1–B17, licence table (privacy.sexy AGPL, CIS CC BY-NC-SA, STIG public domain, LGPO not redistributable), level/profile model, enterprise must-have checklist, UX patterns; ~198 sources | ~140 KB |
| `03-windows-update-control.md` | 58-row Windows Update control table (Settings pause keys, WUfB policies, AU, deadlines, DO, WUA COM hide/unhide, Store/Edge/MSRT), Home-vs-Pro truth table, EOS engine (24H2 Home/Pro EOS 2026-10-13), explicit refusal list, safe reset procedure | ~40 KB |
| `04-security-hardening-catalog.md` | 241 hardening rows across 17 domains incl. all 19 ASR rules, 31 audit subcategories, 28 read-only health checks, level model, compliance-mode design; baselines: MS Security Baseline 25H2, DISA STIG V2R8 (2026-07-01), CIS Win11 Enterprise v5.1.0 (IDs only) | ~176 KB |
| `05-performance-debloat-catalog.md` | 71 Appx rows (~125 package families) with KEEP/SAFE/CAUTION/NEVER, 50 optional features, 219 services with default start type and recommendation, 50 scheduled tasks, 102 real tweaks, 50 snake-oil items with reasons | ~150 KB |
| `06-stack-and-tooling-verification.md` | Every version pin with source and date; incompatibility traps (x86 dotnet PATH, VS 2022 cannot load SDK 10.0.400, `COMReference` breaks under `dotnet build`, `dotnet package list --vulnerable` always exits 0, NuGet `auditSources` must be a service index, FluentAssertions 8 licence, Artifact Signing pricing) | ~74 KB |
| `07-enterprise-safety-architecture.md` | Policy vs preference writes, `Registry.pol`/PReg format, `IGroupPolicyObject`, MDM detection, all-users hive loading, journal/rollback design, drift/remediate contract, Event Log plan, profile signing, elevation & interactive-user targeting, native API table | ~83 KB |

Items the researchers could not verify are tagged `UNVERIFIED`/`verify` inside
each report; catalogue entries derived from them must be verified on the lab VM
before they leave `draft` state.

## 7. Version and gate verification log

### 7.1 Pinned versions (verified against nuget.org / winget / GitHub on 2026-08-16)

| Component | Version | Where pinned |
| ----------- | --------- | -------------- |
| .NET SDK | 10.0.400 (`rollForward: latestPatch`) | `global.json` |
| System.CommandLine | 2.0.11 | `Directory.Packages.props` |
| CommunityToolkit.Mvvm | 8.4.2 | same |
| Microsoft.Extensions.DependencyInjection / Logging.Abstractions | 10.0.11 | same |
| xunit.v3 | 4.0.0 | same |
| Microsoft.Testing.Extensions.CodeCoverage | 18.10.0 | same |
| Roslynator.Analyzers | 4.16.1 | same (global) |
| SonarAnalyzer.CSharp | 10.32.0.713 | same (global) |
| Meziantou.Analyzer | 3.0.159 | same (global) |
| IDisposableAnalyzers | 4.0.8 | same (global) |
| Microsoft.VisualStudio.Threading.Analyzers | 18.7.23 | same (global) |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.6.0 | same (global) |
| ReferenceTrimmer | 3.5.9 | same (global) |
| DotNet.ReproducibleBuilds | 2.0.5 | same (global) |
| dotnet-reportgenerator-globaltool | 5.5.11 | `.config/dotnet-tools.json` |
| CycloneDX (dotnet tool) | 6.2.0 | same |
| jscpd | 5.0.15 | `package.json` |
| markdownlint-cli2 | 0.23.2 | `package.json` |
| semgrep | 1.173.0 | `build/requirements-tools.txt` |
| gitleaks | 8.30.1 | winget `Gitleaks.Gitleaks` (CI downloads the same version) |
| PSScriptAnalyzer | 1.25.0 | CI step; local `Install-Module` |
| GitHub Actions | checkout v7.0.1, setup-dotnet v6.0.0, setup-node v7.0.0, setup-python v7.0.0, upload-artifact v7.0.1, raven-actions/actionlint v2.2.0 — all pinned to commit SHAs (semgrep `p/github-actions` mutable-tag rule) | `.github/workflows/ci.yml` |

Planned (not yet referenced; add at the milestone that needs them, with the
same verification): TaskScheduler 2.12.2, Microsoft.Windows.CsWin32 0.3.298,
System.Management 10.0.11, Vanara.PInvoke.WUApi 5.0.7 (or hand-written WUA COM
interfaces — `<COMReference>` does not build under `dotnet build`),
Corvus.Json.Validator 5.3.2 or JsonSchema.Net 9.4.0, Serilog 4.4.0 +
Serilog.Sinks.EventLog 4.0.0, FlaUI.UIA3 5.0.0, Stryker.NET 4.16.0, WiX 7.0.0,
Verify.XunitV3 31.28.0.

### 7.2 Gate verification log (each gate seen to FAIL on a broken input, then reverted)

| Gate | Command | Broken input used | Observed |
| ------ | --------- | ------------------- | ---------- |
| format | `dotnet format OpenTheWindows.slnx --verify-no-changes` | 3-space indent + trailing whitespace | exit 2, `error WHITESPACE` |
| build / dead code | `dotnet build -c Release` | unused private field | `IDE0052`, `CA1823`, `S1144` as errors |
| build / banned API | same | `DateTime.UtcNow` in Core | `RS0030` error with the reason text |
| build / unused package | same | unused `PackageReference` | `RT0003` error (ReferenceTrimmer) |
| restore / NuGetAudit | `dotnet restore` | `Newtonsoft.Json 12.0.1` | `NU1903` "Warning As Error … high severity vulnerability" |
| test | `dotnet test` | `Assert.Equal(1, 2)` | exit 2, `failed: 1` |
| coverage | `dotnet reportgenerator … minimumCoverageThresholds:lineCoverage=99` | threshold above actual | exit 1 "line coverage of 88% is below the minimum threshold of 99%" (note: `-settings:` and `settings:` variants exit 0 silently) |
| secrets | `gitleaks dir --exit-code 1` | real ed25519 private key under `temp/ssh` (before the dir allowlist for gitignored dirs) | 26 leaks found incl. the custom `otw-openssh-private-key` rule |
| sast | `semgrep scan -e 'Process.Start(...)' -l csharp --error` | file calling `Process.Start` | 1 finding, exit 1 (registry rulesets `p/csharp`, `p/secrets`, `p/github-actions` load and run; their individual rules were not exercised) |
| duplication | `jscpd --config .jscpd.json .` | duplicated `DoctorService.cs` | "Clone found (csharp)", exit 1 |
| markdown | `markdownlint-cli2` | real MD060/MD034 issues in early docs | exit 1, then fixed |
| powershell | `Invoke-ScriptAnalyzer … -Settings PSScriptAnalyzerSettings.psd1` (fail on any record) | `ls C:\` alias in a script | `PSAvoidUsingCmdletAliases`, gate FAIL |

Two decorative-gate traps found and fixed during M0: (1) `-EnableExit` inside
the runner script set a host exit code that the runner overwrote (gate reported
PASS with findings) — the runner now fails on any PSSA record; (2) the template
compatibility profile `core-7.4.0-windows` does not exist in PSSA 1.25 and makes
`Invoke-ScriptAnalyzer` throw a `NullReferenceException` — replaced with the
shipped `core-6.1.0-windows` profile.

### 7.3 Escape hatches register (every suppression, with reason)

| Where | What | Why | Revisit |
| ------- | ------ | ----- | --------- |
| `Directory.Build.props` | `NoWarn CS1591` | XML doc on every public member is busywork; other doc rules stay on | never |
| `Directory.Build.targets` | `NoWarn CA2007` for Exe/WinExe | ConfigureAwait not wanted at UI/CLI entry points | never |
| `OpenTheWindows.App.csproj`, `App.Tests.csproj` | `NoWarn WPF0001` | Fluent `ThemeMode` is `[Experimental]` in .NET 10 WPF; accepted (ADR 0001) | when WPF drops the attribute |
| `.editorconfig` | `IDE0058` silent | fires on every fluent-builder call; CA1806 covers the meaningful cases | never |
| `.editorconfig` | `CA1303` none | user-facing strings will use resx at M6; logs are English | M6 |
| `.editorconfig` | `CA1848` suggestion | LoggerMessage delegates only for hot paths | M6 |
| `.editorconfig` | `CA1812` suggestion | DI-created types | never |
| `.editorconfig` `[tests/**]` | CA1707, CA2007, CA1515, CA1861, CA1062, CA1812, CA1822 none | test conventions | never |
| `PSScriptAnalyzerSettings.psd1` | `PSUseShouldProcessForStateChangingFunctions`, `PSAvoidUsingWriteHost`, `PSAvoidUsingPositionalParameters` excluded | imperative build scripts calling native tools | never |
| `.markdownlint-cli2.jsonc` | `docs/research/**` ignored | generated reference reports with wide tables/raw URLs | never |
| `.gitleaks.toml` | dir-scan allowlist for `.venv-tools/`, `node_modules/`, `artifacts/`, `temp/` | gitignored, third-party or local-only; history scan unaffected | never |
| `BannedSymbols.txt` | applies to `src/` only | tests use HKCU sandbox keys and DateTime in assertions | never |

## 8. Milestones

Every milestone: goal · scope · files · steps · acceptance criteria · tests ·
gates · docs · security checks · performance checks · certification checklist.
**A milestone is not complete until its certification checklist passes.**

### M0 — Scaffold, gates, research (DONE 2026-08-16)

- Goal: production-grade repository from commit one; research complete.
- Scope/files: everything currently in the repository (see `README.md`
  "Project structure").
- Acceptance: all gates green on the scaffold; `otw doctor` runs on the dev box
  and on the 25H2 lab VM; GUI launches elevated and renders the system check;
  seven research reports written.
- Certification checklist:
  - [x] `pwsh build/quality.ps1` — all 10 gates PASS
  - [x] every gate proven to fail on a broken input (§7.2)
  - [x] 43 tests pass; coverage 88% line / 83% branch (thresholds 85/75)
  - [x] `otw doctor` (text + `--json`) exit 0 locally; exit 0 on VM 25H2 (elevated)
  - [x] GUI screenshot captured (`build/smoke-gui.ps1`)
  - [x] publish: `otw.exe` single-file trimmed 13 MB (x64); App self-contained 158 MB unpacked
  - [x] SBOM + SHA256SUMS produced
  - [ ] CI workflow run green on GitHub — **not yet possible: repository not pushed** (deferred, see §11)

### M1 — Catalogue model, schema, loader, `catalog` commands

- Goal: the data model from §5.2 as C# records + JSON Schema; embedded built-in
  catalogue loader with directory overrides; catalogue tests.
- Scope: `Core/Catalog/*` (records, `CatalogLoader`, `CatalogValidator`,
  `CatalogSchema` embedded), `catalog/schema/tweak.schema.json`,
  `catalog/**` (start with ~20 entries across all six categories, all
  `verifiedOn` empty until M3), CLI `otw catalog list|show|validate`,
  `--json`.
- Steps: schema → records → loader → validator rules (unique ids, ≥1 source,
  revert path, known action kinds, level/risk set, `appliesTo` sane) → CLI →
  tests → docs.
- Acceptance: `otw catalog validate` exits 0 on the built-in catalogue and 4 on
  a broken file; every entry renders `otw catalog show <id>` with sources.
- Tests: schema tests, validator negative cases (one per rule), loader
  override/merge tests, CLI tests.
- Gates: all + new `catalog` gate = `otw catalog validate` in `quality.ps1`.
- Docs: `docs/catalog-format.md`, README commands, CHANGELOG.
- Security: schema forbids unknown action kinds; `command` kind allow-list
  documented; no path traversal in override loading (tests).
- Performance: loading 1 000 entries < 200 ms (benchmark test).
- Certification: gates green; catalogue gate proven to fail; docs updated.

### M2 — Detection engine (`scan`) and health checks

- Goal: read-only state readers for every action kind + 28 health checks
  (research 04 §5) + drift report; exit codes 0/1/2.
- Scope: `Core/Engine/Scan*`, `Windows/Readers/*` (registry incl. `HKU\<SID>`,
  services, tasks, Appx, features, Defender WMI, powercfg, firewall, audit
  policy, secedit), `Core/Reports/*` (JSON/CSV/HTML/SARIF), CLI `otw scan
  --profile <p> [--json|--sarif|--html]`, `otw health`.
- Acceptance: `otw scan` on the VM reports drift for a known-changed key and 0
  when compliant; managed settings show `managedBy`.
- Tests: reader unit tests with fakes; Windows integration tests behind
  `OTW_INTEGRATION=1` and elevation; report golden files (Verify).
- Gates: all; add Stryker mutation run for Core (report only at first,
  threshold at M3).
- Security: readers are read-only (analyzer-enforced: no write APIs in
  `Windows/Readers`); SARIF output validated against schema.
- Performance: full scan of ~500 entries < 5 s on the VM.
- Certification: VM run recorded in `docs/certification/M2.md` with output.

### M3 — Apply/verify/revert engine, journal, restore points

- Goal: transactional apply per §5.3; journal at
  `%ProgramData%\OpenTheWindows\journal`; `otw apply`, `otw revert`,
  `otw history`, `--what-if`, System Restore integration; Event Log source.
- Scope: `Core/Engine/Apply*`, `Core/Journal/*`, `Windows/Writers/*` (registry
  incl. GPO policy path via `IGroupPolicyObject` + PReg fallback, service start
  type, tasks, Appx removal/deprovision + `Deprovisioned` marker, features,
  Defender preferences, powercfg, firewall, auditpol/secedit), Explorer restart
  for the interactive session, Event Log source registration (installer/first
  elevated run).
- Acceptance: on the VM, apply a Balanced privacy profile → verify → revert →
  verify original state byte-for-byte per journal; a forced mid-apply failure
  rolls back completely; a policy write survives `gpupdate /force`.
- Tests: engine unit tests with an in-memory OS fake (ordering, rollback,
  idempotency, journal hash chain); integration tests on VM (documented runbook
  `build/vm.ps1` extension + checkpoint/restore per Q1).
- Gates: all; coverage thresholds raised to 90/80; Stryker threshold ≥ 70 on
  Core engine.
- Security: journal ACL (Administrators + SYSTEM only); no writes without a
  journal record (fault-injection test); refuse-list of protected services
  enforced by tests; break-glass requires flag + logs an Event ID 4xxx.
- Performance: apply of 100 registry entries < 3 s; journal write O(n).
- Certification: `docs/certification/M3.md` with VM evidence and screenshots of
  Event Viewer entries.

### M4 — Catalogue population (privacy, updates, security, perf/debloat)

- Goal: convert research 01/03/04/05 into catalogue entries with sources,
  levels, risk, `appliesTo`, `verifiedOn` (verified on the lab VM 25H2 and at
  least one 24H2 image; Home edition rows marked best-effort where policies are
  Pro+ only).
- Scope: `catalog/privacy/*.json`, `catalog/updates/*.json`,
  `catalog/security/*.json`, `catalog/performance/*.json`,
  `catalog/debloat/*.json`, `catalog/shell/*.json`; the explicit refusal list
  as `docs/refusals.md`; the snake-oil list as `docs/not-included.md`.
- Acceptance: ≥ 300 entries; 100% schema-valid; every `Balanced`-or-lower
  entry verified on the VM (apply → verify → revert) with evidence links;
  Windows Update guardrails (35-day cap, EOS warning, resume-now) implemented
  and tested; Defender signature updates provably unaffected by any entry.
- Tests: catalogue tests (per-rule), per-entry integration test generated from
  `verifiedOn` expectations, EOS engine unit tests with fixed clock.
- Gates: all + catalogue gate; markdownlint on the new docs.
- Security: security-category entries map to MS baseline / STIG / CIS IDs;
  a "read-only compliance score" mode exists (`otw audit --baseline ms-25h2`).
- Performance: entries with `measurableBenefit=false` are excluded from
  Balanced or lower.
- Certification: `docs/certification/M4.md` (verification matrix build ×
  edition × entry).

### M5 — Profiles, levels, all-users scope, MDM/GPO awareness, drift task

- Goal: named profiles + custom profiles (JSON, optional ES256 signature and
  pinned org keys), per-category level dial, all-users application (live
  hives, loaded hives, Default profile, Active Setup), managed-setting
  detection, scheduled re-enforcement (`otw remediate`) with build-change
  trigger, Intune detection/remediation script templates.
- Acceptance: signed profile with a wrong key is rejected; managed value is
  reported and not written; all-users apply reaches a logged-off profile and a
  new user; the scheduled task re-applies after a simulated build change.
- Tests/gates/docs/security/perf per pattern; `docs/enterprise.md`.
- Certification: `docs/certification/M5.md`.

### M6 — WPF GUI

- Goal: full GUI: navigation by category, level dials, tweak list with search,
  filter, risk badges and "managed" badges, per-tweak info panel with sources,
  what-if diff view, apply progress with per-item status, history/undo
  timeline, health dashboard, profile import/export, settings; accessibility
  (UIA names, keyboard, high contrast), dark/light, DPI; resx localisation
  infrastructure; "Can apply changes: True" → "Yes" (M0 polish item).
- Tests: view-model tests; FlaUI smoke on the VM (launch, navigate, apply a
  Safe tweak, revert); screenshot set per page in `docs/certification/M6.md`.
- Gates: all + UI smoke; performance: cold start < 2 s on the VM, list of 500
  entries virtualised (< 16 ms frame).
- Certification: manual accessibility check record + screenshots.

### M7 — Packaging and release

- Goal: MSI (WiX 7; per-machine, Event Log source, Start menu, optional PATH
  for `otw`), portable ZIP, winget manifest, release workflow with SBOM,
  SHA256SUMS, GitHub build-provenance attestation, `CHANGELOG`-driven notes;
  documentation for organisations to re-sign and to author WDAC/AppLocker
  rules; framework-dependent variant (Q3).
- Acceptance: MSI installs/uninstalls cleanly on the VM (Home + Pro images),
  `winget validate` passes, attestation verifies with `gh attestation verify`.
- Certification: `docs/certification/M7.md`.

### M8 — Hardening audit mode and reports

- Goal: `otw audit` scoring against MS baseline 25H2 / STIG V2R8 / CIS section
  IDs (IDs only), HTML/JSON/CSV/SARIF exports, compliance report signing (SHA-
  256 content hash), scheduled report drop for fleet collection.
- Certification: `docs/certification/M8.md`.

Backlog (post-M8): PowerShell module wrapping the CLI, WinUI 3 front-end
experiment, ARM64 device smoke, Sysmon helper (Q5), localisation packs.

## 9. Security gates (always on)

- Analyzers at max, security CA rules as errors (`.editorconfig`).
- `NuGetAudit` all/low as errors; locked restore in CI.
- gitleaks (history + tree) and semgrep in CI and in `quality.ps1`.
- Banned APIs: `Registry.CurrentUser`, `Environment.Exit`, `DateTime.Now/UtcNow`,
  `Process` outside the guarded runner.
- Journal-first invariant (M3), protected-service refusal list (M3),
  profile-signature verification (M5).
- SBOM + hashes + attestations on every release (M7).
- Residual risk accepted: unsigned binaries (ADR 0003); SmartScreen friction;
  WDAC/AppLocker sites must add hash rules or re-sign.

## 10. Performance gates

| Budget | Value | Verified |
| -------- | ------- | ---------- |
| `otw.exe` single-file, trimmed, x64 | ≤ 20 MB (measured 13.0 MB) | M0 |
| App self-contained unpacked | ≤ 180 MB (measured 158 MB); framework-dependent variant planned (Q3) | M0 |
| `otw --version` cold | < 300 ms | M1 (add timing test) |
| GUI cold start on VM | < 2 s | M6 |
| Full scan (~500 entries) | < 5 s | M2 |

## 11. Deferred gates and known gaps (honest list)

| Item | Status | Reason / plan |
| ------ | -------- | --------------- |
| CI workflow green on GitHub | **deferred** | repository not yet pushed; `ci.yml` authored, actions pinned, will be verified on first push |
| CodeQL workflow | not added | add with the first push (needs the repository on GitHub); semgrep covers SAST locally meanwhile |
| semgrep registry rules individually verified | partial | mechanism verified with an inline rule; ruleset content is Semgrep's |
| ARM64 runtime smoke | deferred | no ARM64 device; CI publishes win-arm64 |
| VM checkpoint/restore automation | open (Q1) | needs hypervisor API access |
| Mutation testing (Stryker) | M2 | not yet installed |
| FlaUI UI automation | M6 | not yet installed |
| Visual Studio 2022 on the dev box cannot open the solution (SDK 10.0.400 needs VS 18 / VS 2026) | note | CLI/VS Code work; install VS 2026 for the IDE experience |

## 12. Documentation requirements and definition of done

- `README.md`: only commands that have been run; structure, gates, env vars,
  troubleshooting kept current.
- `CHANGELOG.md`: Keep a Changelog; every meaningful change; never rewritten.
- `PLAN.md`: decisions, open questions, escape hatches, milestone status.
- `docs/adr/`: one ADR per shaping decision; superseded ADRs stay.
- `docs/research/`: reference reports; regenerated only deliberately.
- Definition of done for any change: gates green (`pwsh build/quality.ps1`),
  tests for the behaviour (including a negative case), docs updated, CHANGELOG
  entry, no new escape hatch without a row in §7.3.
