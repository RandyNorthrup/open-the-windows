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
Serilog 4.4.0 + Serilog.Sinks.EventLog 4.0.0, FlaUI.UIA3 5.0.0, WiX 7.0.0,
Verify.XunitV3 31.28.0.

Stryker.NET 4.16.0 is pinned in `.config/dotnet-tools.json` with a config
(`stryker-config.json`) scoped to the M2-added Core logic (engine, reports,
policy parser), wired as the opt-in `pwsh build/quality.ps1 -Only mutation`
step (report-only in M2: `break=0`, threshold enforced from M3; never part of
`all`). **Deferred gate:** Stryker.NET does not yet support
Microsoft.Testing.Platform test projects (stryker-mutator/stryker-net#3094), and
this repo mandates MTP (no VSTest packages), so mutation testing cannot run its
tests today. The gate detects exactly this condition and reports DEFERRED rather
than a spurious failure (any other Stryker error still fails it); it also sets
`DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` so Buildalyzer's VS MSBuild can resolve the
.NET 10 SDK. The step activates unchanged once Stryker adds MTP support.

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
| read-only guard (M2) | `Windows_assembly_references_no_write_apis` test (metadata scan of the Windows assembly's member references) | temporary `RegistryKey.SetValue` call added to a reader | test FAIL: "Write API(s) referenced: RegistryKey.SetValue"; reverted, green again |

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
| `.editorconfig` `[tests/**]` | CA1707, CA2007, CA1515, CA1861, CA1062, CA1812, CA1822 none; `async_methods_suffixed` naming rule none | test conventions (async test methods use descriptive sentence names, not the `Async` suffix) | never |
| `PSScriptAnalyzerSettings.psd1` | `PSUseShouldProcessForStateChangingFunctions`, `PSAvoidUsingWriteHost`, `PSAvoidUsingPositionalParameters` excluded | imperative build scripts calling native tools | never |
| `.markdownlint-cli2.jsonc` | `docs/research/**` ignored | generated reference reports with wide tables/raw URLs | never |
| `.jscpd.json` | markdown excluded from `format`; `catalog/**` and `docs/enterprise/*.ps1` ignored | the Linux tokenizer reports ```` ```text ```` code fences as clones across unrelated docs; catalogue entries are declarative data with intentionally uniform action blocks (not copy-pasted logic); Intune uploads each detection/remediation script standalone, so their otw.exe bootstrap is duplicated per file rather than shared through a module | never |
| `.gitleaks.toml` | dir-scan allowlist for `.venv-tools/`, `node_modules/`, `artifacts/`, `temp/` | gitignored, third-party or local-only; history scan unaffected | never |
| `BannedSymbols.txt` | applies to `src/` only | tests use HKCU sandbox keys and DateTime in assertions | never |
| `Directory.Build.props` | `EnableReferenceTrimmer=false` when `MSBuildProjectName` ends with `_wpftmp` | the WPF markup-compile pass builds a generated `<Project>_<hash>_wpftmp.csproj` that omits the code-behind using Core/Windows; ReferenceTrimmer then falsely reports those refs as removable (RT0002), failing the temp build under warnings-as-errors and cascading to BG1002 in the real build. RT still runs on the real App project | when the WPF SDK stops leaking analyzers into the temp project |
| `AppInfo.HomepageUri` (`SuppressMessage`) | Sonar `S1075` (URIs should not be hardcoded) | the project homepage is an immutable product-identity constant used as the SARIF tool `informationUri`, not environment-specific configuration that S1075 targets | never |
| `Profile` record (`SuppressMessage`) | `CA1724` (type names should not match namespaces) | the domain concept is a profile and the M5 spec names the type `Profile`; the only clash is the legacy ASP.NET `System.Web.Profile` namespace, which this net10.0 assembly never references | never |
| `WindowsCommandRunner.cs` (`#pragma RS0030`) | `System.Diagnostics.Process` ban | the guarded command runner is the single sanctioned process host; an architecture test asserts no other `src/` file suppresses RS0030 | never |
| `ApplyEngine.Capture` (`SuppressMessage CA1031`) | catch general exception | the transactional apply/revert boundary must catch any writer failure (Win32/COM/WMI/IO) to journal it and roll back rather than leave a partially-applied run | never |
| `LocalGroupPolicy.RunSta` (`SuppressMessage CA1031`) | catch general exception | the STA worker must marshal any COM/registry failure back to the calling thread as one fault | never |
| `LocalGroupPolicy.Open` (`UnconditionalSuppressMessage IL2072`) | trimmer reflection pattern | activates a fixed in-box COM server (GPClass) by CLSID; the OS COM class is not managed code the trimmer can remove | never |
| `LocalGroupPolicy.MachineRoot` (`SuppressMessage CA2000`, `IDISP001`) | dispose created object | `RegistryKey.FromHandle` takes ownership of the `SafeRegistryHandle`; the caller's `using` disposes both | never |
| `WindowsAppxWriter.Await` (`#pragma VSTHRD002`) | synchronous wait on async | `IAppxWriter` is a synchronous contract; the WinRT deployment operation is awaited to completion on a console/service thread with no synchronization context, so it cannot deadlock | never |
| `WpfDispatcherService.cs` (`SuppressMessage VSTHRD001`) | VS-threading legacy thread-switching API | this type is the single sanctioned UI-thread marshalling boundary for the GUI; `JoinableTaskFactory.SwitchToMainThreadAsync` would add the VS-Threading package with no benefit for a standalone WPF process, and every view model marshals through this one class | never |
| `build/quality.ps1` coverage `classfilters` | `OpenTheWindows.App.Services.DialogService`, `WpfDispatcherService`, `ApplyFlowLauncher` excluded | thin WPF wrappers that only show `MessageBox`/`Dispatcher`/`Window` chrome, with no branch logic; not unit-testable without a live UI thread, matching the existing Windows UI/OS-wrapper exclusions | never |
| `ApplyFlowViewModel.Start`, `ApplyFlowViewModel.ApplyAsync` (`SuppressMessage CA1031`) | catch general exception | the review-and-apply UI boundary must land any engine/read fault in a Failed state with the message rather than crash the window; operational failures are already journaled and rolled back by the engine (same rationale as `ApplyEngine.Capture`) | never |
| `build/quality.ps1` coverage `filefilters` | `-*.xaml.cs` (WPF code-behind) | code-behind is `InitializeComponent` constructors and event plumbing with no testable logic; the DI graph in `DesktopApp.xaml.cs` stays verified by `ServiceGraphTests` (test gate), just not measured by the coverage gate | never |

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
| M1 | [docs/milestones/M1-catalogue.md](docs/milestones/M1-catalogue.md) — catalogue model, schema, loader, `otw catalog` | DONE 2026-08-16 | `docs/certification/M1.md` |
| M2 | [docs/milestones/M2-scan.md](docs/milestones/M2-scan.md) — detection engine, health checks, reports | DONE 2026-08-16 | `docs/certification/M2.md` |
| M3 | [docs/milestones/M3-apply.md](docs/milestones/M3-apply.md) — apply/verify/revert, journal, restore points, Event Log | DONE 2026-08-17 | `docs/certification/M3.md` |
| M4 | [docs/milestones/M4-catalogue-population.md](docs/milestones/M4-catalogue-population.md) — ≥ 300 entries + VM verification + WU guardrails | in progress | pending |
| M5 | [docs/milestones/M5-profiles-enterprise.md](docs/milestones/M5-profiles-enterprise.md) — profiles, all-users, MDM/GPO awareness, drift task | DONE 2026-08-19 | `docs/certification/M5.md` |
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

### M2 — Scan (DONE 2026-08-16)

### M3 — Apply (DONE 2026-08-17)

See `docs/milestones/M3-apply.md`; certification in `docs/certification/M3.md`.
Transactional `ApplyEngine` (plan → preflight → optional restore point →
journal-first → apply in dependency order → verify → roll back on any failure)
with `ActionApplier` executing all eight action kinds; `RevertEngine` behaviour
folded into `ApplyEngine.Revert`, replayed from the journal alone; a
hash-chained journal (`Core/Journal`, `docs/journal-format.md`); the full writer
set + `LocalGroupPolicy` (COM `IGroupPolicyObject`), restore-point, Event Log +
JSONL audit (`docs/event-log.md`), pre-flight and Explorer-restart Windows
adapters; `otw apply` / `revert` / `history`. Coverage thresholds raised to
90/80 with the OS-mutating `Writers`/`Interop` namespaces, the restore-point /
explorer services and the environment-dependent `WindowsMachineHealthProbe`
scoped out of coverage (each verified by the VM integration tests,
`OTW_INTEGRATION=1`, not by units; the health probe's per-check threshold and
exception branches are structurally unreachable on any single machine run).

**Deviations (accepted):**

- **Optional features** use the in-box `dism.exe` through the guarded command
  runner rather than a hand-rolled DISM API binding — same effect, no fragile
  marshalling (`dism.exe` is on the command allow-list).
- **Local GPO** uses classic `[ComImport] IGroupPolicyObject` (declaring only
  the three used v-table slots, the rest as ordered placeholders) on an STA
  thread; policy values are saved to `Registry.pol` and mirrored to the live
  hive for immediate effect (no per-write `RefreshPolicyEx`).
- **Explorer restart** targets the active console session (the interactive user
  in the single-user case); multi-session SID-precise restart is a later
  refinement.
- **Per-user Local GPO (MLGPO)** is still deferred to M5; per-user policy values
  are mirrored directly to `HKU\<sid>\Software\Policies`.
- The **mutation gate stays DEFERRED** (Stryker.NET has no Microsoft.Testing.Platform
  support, stryker-net#3094); the M3 spec's "mutation now enforcing" cannot hold
  until that lands, so the gate still reports DEFERRED rather than a false pass.

### M4 — Catalogue population (in progress)

Landed so far (branch `feature/m4-catalogue-population`, committed locally):

- **Windows Update guardrails** (`Core/Updates`): `PauseCalculator` (six
  Settings-app pause values as ISO-8601 UTC, 35-day cap, opt-in extended pause to
  a configurable ceiling, default 365), `EndOfServicingCalendar` (embedded
  `catalog/updates/eos.json`, per-track days-to-EOS, 90-day warning), and
  `UpdateControlService` (pause / resume / status all routed through the apply
  engine so they are journaled and reversible; extended pause writes Windows
  Event 4002; resume reverts the latest Open the Windows pause and triggers
  `usoclient StartScan`). New CLI `otw updates pause|resume|status` and a
  `--only <id>` selector on `scan`/`apply`. Wired by
  `WindowsUpdateControlFactory`.
- **Defender safety boundary** (`Core/Engine/DefenderSafety`): the engine refuses
  any registry write under `Windows Defender\Signature Updates` and any Defender
  preference that disables a protection or signature updates (audited refusal);
  `usoclient.exe` added to the command allow-list with a validator test.
- **Refusal / not-included docs**: `docs/refusals.md` and
  `docs/not-included.md`, linked from the README.
- **Catalogue authoring started (WP 4.2 updates)**: 18 new Windows Update control
  entries in `catalog/updates/windows-update-controls.json` (deferral presets,
  release pin, Delivery Optimization modes, restart deadlines, AU behaviour, MSRT
  and Store update opt-outs, notifications), transcribed from research 03 §5.2–5.12
  / §6; all Draft, `otw catalog validate` clean.
- **Security hardening catalogue (WP 4.3)**: 147 new Security entries across seven
  domain files under `catalog/security/` (credential/identity, UAC/logon,
  network/firewall/remote, Defender + 18 ASR rules, exploit/app-control,
  audit/media/crypto, boot/features), transcribed from research 04 §4.A–§4.Q with
  exact registry/DefenderPreference/Command mechanisms, edition/build gating,
  `managedBy` and `baseline`/`stig`/`cis`/`acsc` provenance tags. chk-only health
  checks, domain-only settings and no-fallback refusals were excluded; every entry
  was adversarially re-verified against its research section (value, hex, hive,
  type, ASR-GUID fidelity). Catalogue now **194 entries** (153 Security), all
  Draft, `otw catalog validate` clean, 0 warnings. A `BuiltInCatalogTests` guard
  asserts the ≥120 Security target.
- **Telemetry / privacy catalogue (WP 4.1)**: 98 new Privacy entries across six
  domain files under `catalog/privacy/` (diagnostic-data / CEIP / WER, advertising
  ID and nags, cloud content / Spotlight / widgets, search / Copilot / AI /
  activity history, input personalization and app-permission consent, sync / Store
  / consumer features, Edge telemetry and scheduled tasks), transcribed from
  research 01 §1–§16 with the policy-vs-preference distinction, per-user (`User`)
  vs machine scope, and every **Never** (security-regression) / **n/a** row
  skipped. A full-catalogue action-target collision scan removed 12 privacy and 3
  security duplicates of existing controls; each file was adversarially
  re-verified. Catalogue now **289 entries** (107 Privacy, 150 Security, 22
  Updates), all Draft, `otw catalog validate` clean, 0 warnings; a
  `BuiltInCatalogTests` guard asserts the ≥90 Privacy target. **Deferred:** local
  account / password /
  lockout policy (research §4.B) is not authorable in the closed action set — a
  bare `secedit.exe` Command cannot stage its `.inf` — and waits on a dedicated
  security-policy action kind (candidate for a later milestone) rather than
  shipping non-functional command entries. Fixed on the way: `otw apply --what-if`
  now returns 0 for a clean preview even when the plan contains reboot-required
  entries (the restart requirement is still reported in output).

- **Performance / debloat / shell catalogues (WP 4.4 / 4.5 / 4.6)**: 143 new
  entries from research 05 — debloat inbox-app / component / optional-feature
  removals (verified `_8wekyb3d8bbwe` family names, no guessed publisher ids),
  performance disable-able services (non-protected only; per-user services via
  their template `Start` key), telemetry-adjacent tasks, gaming and measurable
  visual / power (`powercfg`) / storage (`fsutil`) tweaks, and shell taskbar /
  Start / Explorer UX toggles. Research 05 §7 snake-oil and every
  `NEVER` / `KEEP` / system-app / protected-service row excluded; OEM stubs with
  unknown publisher ids skipped rather than guessed. A collision scan removed nine
  cross-category duplicates and paired the Windows-Ink entries; each file was
  adversarially re-verified (zero discrepancies). Catalogue now **432 entries**
  (Security 150, Privacy 107, Performance 61, Debloat 61, Shell 31, Updates 22),
  `otw catalog validate` clean, 0 warnings; a `BuiltInCatalogTests` theory asserts
  every category's M4 minimum.

Three more Windows Update client-policy entries (Microsoft-Update opt-in, an
18-hour active-hours range, and an auto-download/scheduled-install mode paired
with notify-before-install by `conflictsWith`) bring Updates to 25. **All six
categories now meet their M4 per-category minimums** — Security 150, Privacy 107,
Performance 61, Debloat 61, Shell 31, Updates 25 (**435 entries total**), enforced
by the `BuiltInCatalogTests` theory.

**Acceptance #3 (Windows Update pause / resume / status + EOS) is verified on the
lab VM** (25H2, 26200.9168, elevated) — evidence in
[docs/certification/M4/updates-pause-resume.md](docs/certification/M4/updates-pause-resume.md):
what-if writes nothing; `pause --days 7` writes the six `UX\Settings` values (read
back independently); `pause --days 45` is rejected at the 35-day cap (exit 4);
`pause --days 60 --extended` warns and writes Event 4002 (custom `OpenTheWindows`
log + JSONL trail); `resume` returns all six values to absent and `status` reports
"not paused". This run surfaced and fixed a **stacked-pause resume bug** (resume
had reverted only the most-recent pause, restoring an earlier one) — see the
CHANGELOG; regressioned by `Resume_after_a_restacked_pause_clears_every_active_pause`.

**Acceptance #2 (machine-scope VM verification) DONE** — see
[docs/certification/M4.md](docs/certification/M4.md) and the per-entry transcript
[docs/certification/M4/machine-scope-verification.md](docs/certification/M4/machine-scope-verification.md).
144 Basic/Balanced entries are now `Verified` on 25H2 (131 by full apply →
independent-read → revert → pre-state round-trip, 13 already-compliant). Of the 340
Basic/Balanced entries, 190 were exercisable over the headless SSH session and 150
were deferred (75 per-user hive — the M3 null-interactive-user limitation; 47
network/remote-access/auth entries that would sever the SSH session; 28 Appx
removals that are intentionally not round-trip reversible). Of the 190, the 46 not
verified are documented honestly: Tamper Protection blocks Defender/ASR
`Set-MpPreference` writes (confirmed `IsTamperProtected=True`); two TrustedInstaller
-owned keys deny writes even elevated; sixteen entries target components absent on
this image or are edition-gated to Enterprise/Education; the rest are Local-GPO
`registry.pol` write contention.

**Verification findings → candidate product hardening (§7.3 / follow-up):** the
engine does not retry the Local-GPO `registry.pol` lock (`0x80070020`) that separate
elevated invocations contend on; Defender/ASR configuration via `Set-MpPreference`
is blocked by Tamper Protection and needs a GPO/Intune path; a Policy write over an
OS-default-populated key deletes rather than restores the original value on revert;
`security.media.dma-disable-under-lock` shows a scan-compliance discrepancy (engine
reports compliant while the value is absent).

Remaining for M4: a console-session verification pass for the 150 deferred entries
is M5 work (the interactive-user resolver returns null over headless SSH, and the
network/auth entries need a session that is not the one being reconfigured).

### M5 — Profiles and enterprise (done)

Landed so far (branch `feature/m5-profiles-enterprise`, stacked on the M4 branch,
committed locally):

- **Profile format** (`Core/Profiles`): the `Profile` model (level dial per
  category, `include`/`exclude`, `Scope`, `ProfileOptions`, applicability),
  `catalog/schema/profile.schema.json`, `ProfileLoader` (embedded built-ins /
  file / text, schema-validated before deserialization; the profile schema rides
  under `catalog/schema/` and profiles are embedded under a separate `profiles/`
  logical prefix so the catalogue loader never parses them as tweak documents),
  `ProfileResolver` (levels → risk gate → includes → excludes → per-entry
  applicability, in catalogue order) and `ProfileValidator` (unknown/colliding/
  deprecated include-exclude ids; built-ins never apply Breaking). Seven built-in
  profiles ship embedded; none allows or resolves to a Breaking entry. The JSON
  Schema evaluation shared with the catalogue loader was factored into
  `Core/Catalog/SchemaEvaluation`. `Profile` suppresses `CA1724` (PLAN §7.3).
- **Profile CLI** (`otw profile list|show|validate`): read-only inspection of the
  built-in profiles or an operator profile file (built-in id or path), with
  `--json` on each and schema + catalogue-aware validation on `validate`.
- **Profiles drive scan/apply** (`CommandSupport.TrySelectEntries`): `--profile`
  on `scan`/`apply` now resolves a level name (uniform, as before), a built-in
  profile id, or a profile file — the latter two through `ProfileResolver` using
  the machine facts from `otw doctor`. Only entry *selection* is profile-driven so
  far; a profile's apply `options`/`scope` are not yet consumed (deferred to the
  all-users work package). The engine layer is unchanged (it already takes an
  explicit entry set).
- **Profile signing** (`Core/Profiles/ProfileSignature`, `otw profile sign|verify`):
  detached ES256 (ECDSA P-256 / SHA-256, decision D11) over a canonicalised form
  of the profile JSON; `keyId` = hex SHA-256 of the SubjectPublicKeyInfo; `sign`
  writes `<file>.sig`, `verify` checks a given public key or the machine trust
  store (`%ProgramData%\OpenTheWindows\trusted-keys`).
- **RequireSignedProfiles policy** (`Core/Profiles/ProfilePolicy`, `ProfileTrust`):
  `ProfilePolicy.Read` reads `HKLM\SOFTWARE\Policies\OpenTheWindows\RequireSignedProfiles`
  (REG_DWORD) through the injected `IRegistryReader`; when it is on,
  `CommandSupport` refuses a profile *file* on `scan`/`apply` unless
  `ProfileTrust` verifies its `<file>.sig` against the machine trust store
  (unsigned / unparseable / untrusted-key all refused). Built-in ids and level
  names are exempt — they are not files. The shared trust-store verification was
  extracted into `ProfileTrust` (also used by `otw profile verify`). Ships the
  GPO template `admx/OpenTheWindows.admx` (+ `en-US/OpenTheWindows.adml`) and the
  format reference `docs/profile-format.md`.
- **`apply` honours a profile's options**: a resolved profile's `options`
  (`restorePoint`/`restartExplorer`/`allowAdvanced`/`allowBreaking`) now seed the
  `ApplyOptions`, with the CLI flags still overriding (enable-switches turn on,
  `--no-restore-point` turns off). Fixes the gap where a profile that selected
  Advanced entries (`allowAdvanced:true`) had them blocked at apply unless
  `--allow-advanced` was also passed. `CommandSupport.TrySelectEntries` now yields
  the resolved `Profile`. Still deferred to the all-users work package: consuming
  a profile's `scope` (needs the hive enumerator).
- **`otw remediate`**: the unattended re-enforcement command for automation (the
  drift task and Intune) — scan, then apply only drift, no interactive
  confirmation, never break-glass; `--out` writes the stable JSON report (creating
  its directory), else `--json`/text to stdout; same option surface and exit
  contract as `apply`. The shared apply plumbing (option-building, exit-code
  mapping, JSON/text rendering) was factored into `Cli/Commands/ApplyReporting`,
  now used by both `apply` and `remediate`.
- **Drift scheduled task** (`otw task install|remove`): registers
  `\OpenTheWindows\Remediate` (SYSTEM, highest privileges, hidden) running
  `otw remediate --profile <id> --json --out <reports>\last-remediate.json` on a
  `--daily`/`--weekly`/`--on-logon` schedule; `remove` deletes it; both elevated.
  New Core `IScheduledTaskInstaller`/`ScheduledTaskSpec`/`TaskTrigger` +
  deterministic `RemediationTask` builder (asserted in tests, no Task-Scheduler
  XML golden); Windows `WindowsScheduledTaskInstaller` via the TaskScheduler 2.0
  API (`RegisterTaskDefinition`); new lazy `CliServices.CreateTaskInstaller`;
  `TaskCommand` modeled on `UpdatesCommand`. Self-path via `Environment.ProcessPath`.
- **Enterprise deployment** (`docs/enterprise.md`, `docs/enterprise/*.ps1`): the
  managed-fleet guide and the paired Intune Remediations scripts. `intune-detect.ps1`
  wraps `otw scan` and maps the exit contract onto Intune detection (0 healthy /
  non-zero remediate); `intune-remediate.ps1` wraps `otw remediate` and maps it onto
  Intune remediation (0 success / non-zero failure), writing `last-remediate.json`.
  Each is standalone (Intune uploads detection/remediation separately) and resolves
  `otw.exe` from `$OtwPath`, then PATH, then `%ProgramFiles%\OpenTheWindows`; their
  shared bootstrap is excluded from the duplication gate (§7.3). The guide also
  covers signed profiles + `RequireSignedProfiles`/ADMX and WDAC allow-listing of the
  unsigned binary (file-hash rule, or a signer rule after re-signing).
- **Build-change detection** (`Core.Engine.BuildChange`, `otw remediate
  --if-build-changed`, `otw task install --on-build-change`): `BuildChange.Detect`
  compares the live OS facts (`Build`/`Revision`) against the OS on the most recent
  journal record (`JournalOs`); `--if-build-changed` short-circuits `remediate` to a
  no-op exit 0 when the build is unchanged, and applies when it changed or there is
  no baseline. The CLI reaches the journal store through a new lazy
  `CliServices.CreateJournalStore` (the store was previously buried inside the apply
  engine). `TaskTrigger.BuildChange` maps to a `BootTrigger` (a feature update
  reboots) and `RemediationTask.Build` appends `--if-build-changed` for it, so the
  boot-triggered task re-applies only after a feature update.
- **All-users foundation** (`Core.Abstractions.IUserHiveEnumerator`/`UserProfile`,
  `Windows.Readers.WindowsUserHiveEnumerator`, `otw users`): the read-only first
  slice of all-users scope. The enumerator reads `HKLM\…\ProfileList`, keeps only
  real accounts (`S-1-5-21-…` local/domain and `S-1-12-1-…` Azure AD), skips the
  service accounts and `.DEFAULT`, and reports each `HKEY_USERS\<sid>` hive's loaded
  state; `otw users [--json]` lists them through a new lazy
  `CliServices.CreateUserHiveEnumerator`. VM-verified on 26200.9168 (lists the real
  account, skips system SIDs).
- **All-users journal groundwork** (`JournalAction.Sid`, `ActionApplier.EffectiveSid`):
  the journal now records the SID each action wrote under (user-hive registry /
  per-user Appx; absent for machine scope), and apply/rollback/revert drive each
  action off its own `Sid` rather than one run-level SID. Behaviour-preserving for
  single-user runs; lets one entry be applied across several hives and reverted per
  hive.
- **User-hive loader** (`Core.Abstractions.IUserHiveLoader`,
  `Windows.Writers.WindowsUserHiveLoader`): loads/unloads a user's `NTUSER.DAT` with
  `RegLoadKey`/`RegUnLoadKey`, enabling `SeBackupPrivilege`/`SeRestorePrivilege` via
  `AdjustTokenPrivileges`; mounts under `HKEY_USERS\<sid>` so the existing per-user
  writer targets it unchanged. Explicit `Load`/`Unload` (not `IDisposable`) so unload
  failure surfaces to the engine, not a throwing `Dispose`. VM-verified on 26200.9168
  (loads + unloads the Default profile hive; no `GC.Collect` needed).
- **All-users apply** (`ApplyOptions.Scope`, `ApplyEngine`): a `scope: AllUsers`
  profile applies each user-scoped entry to every real user hive. The engine
  enumerates the target users, loads the hives of any not logged on (unload in a
  `finally`; an unload failure is audited, not fatal), and — on the per-action SID
  groundwork — journals one action per hive. `Classify` plans a user-scoped entry
  even when the interactive user is compliant (`ActionApplier.IsUserScoped`), and
  each hive is decided independently by reading it (compliant skipped, drifted
  applied and revertible per hive). `ApplyServices` gains the enumerator + loader
  (wired in `WindowsApplyEngineFactory`); `apply`/`remediate` pass `profile.Scope`
  via `ApplyReporting.BuildOptions`. Comprehensive engine unit tests (expansion,
  offline-hive load/unload, per-user drift, unload-failure audit). VM-verified on
  26200.9168: a real `scope: AllUsers` apply through the production factory writes a
  user-scoped value to the current user's hive and reverts it.
- **Active Setup logon fallback** (`Core.Abstractions.IActiveSetupRegistrar` +
  `ActiveSetupComponent`/`ActiveSetupResult`, `Core.Engine.ActiveSetupFallback`,
  `Windows.Writers.WindowsActiveSetupRegistrar`, `otw remediate --user-scope`): an
  all-users `apply`/`remediate` that completes now also registers an Active Setup
  component (`HKLM\SOFTWARE\Microsoft\Active Setup\Installed Components\{OTW-<id>}`,
  `StubPath = otw remediate --user-scope --profile <id>`, `Version` bumped per
  apply), so logged-off and future users are re-enforced in their own session at
  next sign-in; registration is skipped while OOBE is in progress
  (`HKLM\SYSTEM\Setup\OOBEInProgress`) and the profile id is validated to kebab-case
  before it forms the key path. `remediate --user-scope` is the stub's mode: it
  applies only a profile's user-scope entries (every action user-scoped) to the
  caller's own hive **without elevation** — writing one's own `HKU\<SID>` needs no
  admin token, so `IPreflight.Run` is now scope-aware (elevation required for
  `Machine`/`AllUsers`, exempt for `Scope.User`). `otw task remove` also clears the
  logon fallback (`RemoveAll`). Engine/CLI/pure unit tests plus a VM check on
  26200.9168 (register → verify values → bump → remove).
- **All-users revert reloads offline hives + managed-value acceptance**
  (`ApplyEngine.Revert`): reverting an all-users apply now reloads the hives of
  users who have logged off since the run (the SIDs on the journalled actions,
  matched to the enumerator's offline profiles) so it restores and unloads each —
  the apply stays reversible after a user signs out. VM-verified on 26200.9168: an
  all-users apply reaches a genuinely offline profile (a synthetic ProfileList
  entry over a copy of the Default hive), writes it, and revert reloads and
  restores it; and a Group-Policy-managed value (owned by the Local GPO
  Registry.pol) is reported Managed and left untouched by apply.
- **Profiles never resolve to a conflict** (`ProfileResolver`): fixed a defect where
  five of seven built-in profiles could not apply (exit 4) because mutually-exclusive
  update presets shared `level: Balanced`. The resolver now drops conflicting entries
  from the selected set deterministically (an explicit `include` wins, else the higher
  level, else catalogue order), so every profile always yields an applicable plan; the
  catalogue was re-levelled to a single Balanced feature-update default (30-day defer;
  90-day and the 25H2 pin are opt-in at Strict) and the Delivery Optimization mode is
  chosen per-profile via `include` (LAN-only for enterprise-workstation/kiosk-shared,
  HTTP-only for privacy-max/developer/power-user). Tests assert no built-in resolves to
  a conflict and that the Balanced defaults are exactly these; VM-verified on 26200.9168
  (all seven built-ins `apply --what-if` exit 0). Still pending for M5:
  `docs/certification/M5.md` (+ evidence subfiles like M4's).

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
