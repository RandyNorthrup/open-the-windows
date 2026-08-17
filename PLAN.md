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
2. **Pausing Windows Update is a core feature; permanently breaking it is
   not.** Pause, hold, defer, target and schedule updates through the
   mechanisms Windows honours (Settings pause keys, WUfB policies, AU options,
   Delivery Optimization, per-update hide) — including extended pauses beyond
   the 35-day UI cap at Advanced tier with clear warnings and a "resume now"
   button — while keeping Defender signature updates flowing. What is refused
   is the self-defeating class of hacks: taking ownership of / ACL-locking
   `WaaSMedicSvc`, setting `wuauserv`/`UsoSvc` to Disabled as an "update
   blocker" (WaaSMedic reverts it and it breaks Store, Defender and optional
   features), `DoNotConnectToWindowsUpdateInternetLocations` on consumer
   machines, and hosts-file blocking (research 03 §"refuse", 05 §7). Defender,
   UAC and PPL/protected services are likewise never disabled.
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
| D21 | Windows Update **pause/hold is first-class**: (a) Settings pause via the `UX\Settings` pause keys, default cap 35 days, **extended pause** (configurable ceiling, default 365 days) at Advanced tier with warnings + reminders + one-click resume; (b) WUfB defer/target/AU/deadline/DO policies; (c) per-update hide via WUA COM; (d) supervised "hold and repair" flow that stops `wuauserv`/`UsoSvc`/`BITS`/`DoSvc` temporarily. Refused: permanent Disabled start types and ownership/ACL hacks on `WaaSMedicSvc`/`UsoSvc` (reverted by WaaSMedic; break Store/Defender/optional features). Defender signature updates are never blocked. | Resolved by owner (2026-08-16: "we do want to be able to pause Windows core services, i.e. Windows Update") | research 03 |

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
detection (`PolicyManager`, RSOP, GP history); Windows Update services
(`wuauserv`, `UsoSvc`, `BITS`, `DoSvc`) may be **stopped temporarily** inside
the supervised hold/repair flow but are never set to Disabled, and
`WaaSMedicSvc`, `TrustedInstaller`, `SecurityHealthService`, `WinDefend`,
`wscsvc`, `EventLog` and other protected/PPL services are never touched; no
hosts-file or firewall blocking of Microsoft endpoints; Windows Update pause
defaults to the 35-day cap with an Advanced-tier extended pause behind a
warning, a configurable ceiling and a "resume now" action; Defender signature
updates never blocked; Edge/WebView2 never removed; OneDrive removal warns
about Known Folder Move; feature-update re-apply via a scheduled task that
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
| System.ServiceProcess.ServiceController | 10.0.11 | same (Windows project; service reads) |
| TaskScheduler | 2.12.2 | same (Windows project; scheduled-task reads) |
| System.Management | 10.0.11 | same (Windows project; WMI optional-feature and Defender reads) |
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
same verification): Microsoft.Windows.CsWin32 0.3.298, Vanara.PInvoke.WUApi
5.0.7 (or hand-written WUA COM interfaces — `<COMReference>` does not build
under `dotnet build`), Corvus.Json.Validator 5.3.2 or JsonSchema.Net 9.4.0,
Serilog 4.4.0 + Serilog.Sinks.EventLog 4.0.0, FlaUI.UIA3 5.0.0, Stryker.NET
4.16.0, WiX 7.0.0, Verify.XunitV3 31.28.0.

M2 deviation from the M2-scan spec: optional-feature state is read via WMI
`Win32_OptionalFeature` (through `System.Management`), not the DISM API the
spec suggested. Opening a DISM online session requires administrator rights and
the DISM API can also modify the image; `Win32_OptionalFeature` is strictly
read-only and queryable unelevated, so the scan needs no elevation for feature
detection and the reader is testable on an unelevated developer box. The
InstallState mapping (1 enabled, 2 disabled, 3/4/absent ⇒ not present) is
documented in `WindowsOptionalFeatureReader`.

M2 native interop: the P/Invoke readers (power via `powrprof.dll`,
interactive user via `wtsapi32.dll`) use the `[LibraryImport]` source generator
rather than the CsWin32 the spec suggested — no build-time code generator
dependency, AOT/trim-safe stubs, and only a handful of functions are needed.
`[LibraryImport]` marshalling stubs pin blittable arguments with unsafe
pointers, so `AllowUnsafeBlocks` is enabled in the Windows project only (no
hand-written unsafe code exists outside the generated stubs); every P/Invoke
carries `[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]` to prevent
DLL search-order hijacking (CA5392).

M2 managed-setting detection: Group Policy ownership is detected by reading the
Local GPO `Registry.pol` files (a new read-only PReg parser,
`OpenTheWindows.Core.Policy.PolicyRegistryFile`) rather than the RSOP
`RSOP_RegistryPolicySetting` query D2 lists. The `.pol` files are world-readable
so the check needs no elevation and is deterministic and unit-testable with
golden fixtures, whereas RSOP requires administrator rights and returns nothing
on an unmanaged machine. Per-value MDM attribution (PolicyManager
`current\device\<Area>\<Policy>` + `_WinningProvider`) and per-user MLGPO
(`GroupPolicyUsers\<sid>`) need identifiers the M2 detector signature/catalogue
do not carry and are deferred to the policy engine (M5); until then a value that
cannot be positively tied to a policy is reported NotManaged, which never hides
real drift. The read-only PReg parser is also the read half of M5's PolicyWriter.

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
| catalog | `otw catalog validate --catalog-dir tests/fixtures/catalog-bad` | entry missing every required field | exit 4, 5 `schema` errors incl. JSON pointers; gate also proven to FAIL when the fixture is replaced with a valid document ("did not reject the broken fixture") |
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
| `.jscpd.json` | markdown excluded from `format`; `catalog/**` ignored | the Linux tokenizer reports ```` ```text ```` code fences as clones across unrelated docs; catalogue entries are declarative data with intentionally uniform action blocks (not copy-pasted logic) | never |
| `.gitleaks.toml` | dir-scan allowlist for `.venv-tools/`, `node_modules/`, `artifacts/`, `temp/` | gitignored, third-party or local-only; history scan unaffected | never |
| `BannedSymbols.txt` | applies to `src/` only | tests use HKCU sandbox keys and DateTime in assertions | never |
| `Directory.Build.props` | `EnableReferenceTrimmer=false` when `MSBuildProjectName` ends with `_wpftmp` | the WPF markup-compile pass builds a generated `<Project>_<hash>_wpftmp.csproj` that omits the code-behind using Core/Windows; ReferenceTrimmer then falsely reports those refs as removable (RT0002), failing the temp build under warnings-as-errors and cascading to BG1002 in the real build. RT still runs on the real App project | when the WPF SDK stops leaking analyzers into the temp project |
| `AppInfo.HomepageUri` (`SuppressMessage`) | Sonar `S1075` (URIs should not be hardcoded) | the project homepage is an immutable product-identity constant used as the SARIF tool `informationUri`, not environment-specific configuration that S1075 targets | never |

## 8. Milestones

The executable specification of every milestone lives in `docs/milestones/`
(one file each); this section is the index and the status board. The runbook
`docs/milestones/README.md` says how to execute a milestone and what the
certification record must contain. **Enforcement:** `pwsh build/start-milestone.ps1
-Milestone <Mx>` must be run before code edits (the Claude Code hook in
`.claude/settings.json` blocks `src/ tests/ catalog/ build/` edits otherwise);
the `plan` gate (`build/check-plan.ps1`, in `quality.ps1`, CI and the git
pre-commit hook) fails when this table, the specs, the certification records
or the agent instruction files disagree.

| # | Spec | Status | Certification |
| --- | ------ | -------- | --------------- |
| M0 | Scaffold, gates, research (recorded inline below) | DONE 2026-08-16 | inline (§8.1) |
| M1 | [docs/milestones/M1-catalogue.md](docs/milestones/M1-catalogue.md) — catalogue model, schema, loader, `otw catalog` | in progress (uncommitted WIP; see spec "Current WIP") | `docs/certification/M1.md` (pending) |
| M2 | [docs/milestones/M2-scan.md](docs/milestones/M2-scan.md) — detection engine, health checks, reports | not started | pending |
| M3 | [docs/milestones/M3-apply.md](docs/milestones/M3-apply.md) — apply/verify/revert, journal, restore points, Event Log | not started | pending |
| M4 | [docs/milestones/M4-catalogue-population.md](docs/milestones/M4-catalogue-population.md) — ≥ 300 entries + VM verification + WU guardrails | not started | pending |
| M5 | [docs/milestones/M5-profiles-enterprise.md](docs/milestones/M5-profiles-enterprise.md) — profiles, all-users, MDM/GPO awareness, drift task | not started | pending |
| M6 | [docs/milestones/M6-gui.md](docs/milestones/M6-gui.md) — WPF GUI | not started | pending |
| M7 | [docs/milestones/M7-packaging.md](docs/milestones/M7-packaging.md) — MSI, ZIP, winget, release workflow | not started | pending |
| M8 | [docs/milestones/M8-audit.md](docs/milestones/M8-audit.md) — baseline audit mode and reports | not started | pending |

Milestone headings below exist for the `plan` gate (`### Mx ...`; "DONE" in
the heading requires `docs/certification/Mx.md`, except M0).

### M0 — Scaffold, gates, research (DONE 2026-08-16)

Certification recorded here (§8.1) because the certification template did not
exist yet; every later milestone uses `docs/certification/Mx.md`.

#### 8.1 M0 certification

- [x] `pwsh build/quality.ps1` — all gates PASS (10 at the time; `plan` added later, PASS)
- [x] every gate proven to fail on a broken input (§7.2)
- [x] 43 tests pass; coverage 88% line / 83% branch (thresholds 85/75)
- [x] `otw doctor` (text + `--json`) exit 0 locally; exit 0 on VM 25H2 (elevated)
- [x] GUI screenshot captured (`build/smoke-gui.ps1`)
- [x] publish: `otw.exe` single-file trimmed 13 MB (x64); App self-contained 158 MB unpacked
- [x] SBOM + SHA256SUMS produced
- [x] CI green on `main` (run 31978989596, 2026-08-16) after four fixes found only by the
  real runner: NU1004 locked-restore vs RID publish, jscpd markdown false clones on Linux,
  plan gate before the schema exists, and `otw doctor` correctly refusing the Windows Server
  runner (now asserted, exit 3)

### M1 — Catalogue (DONE 2026-08-16)

See `docs/milestones/M1-catalogue.md`; certification in `docs/certification/M1.md`.
Catalogue model + JSON Schema + embedded loader (directory overrides) + structural
validator (stable rule ids) + read-only `otw catalog list|show|validate`, 29 Draft
entries, a `catalog` quality gate, and 141 tests (87.7% line / 78.5% branch).

### M2 — Scan (not started)

### M3 — Apply (not started)

### M4 — Catalogue population (not started)

### M5 — Profiles and enterprise (not started)

### M6 — GUI (not started)

### M7 — Packaging (not started)

### M8 — Audit (not started)

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
| CI workflow green on GitHub | **done** (run 31978989596, 2026-08-16; private repo `RandyNorthrup/open-the-windows`) | actions SHA-pinned; both jobs green |
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
