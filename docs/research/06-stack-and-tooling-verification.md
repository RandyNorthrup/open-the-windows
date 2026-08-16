# 06 — Stack and tooling verification (versions, compatibility, recommendations)

- Verified on: **2026-08-16** (all "access date" columns below are this date unless stated)
- Verification method: `dotnet` / `winget` / `gh` / `npm view` on the dev machine, the NuGet v3 registration API
  (`api.nuget.org/v3/registration5-gz-semver2/...`, listed versions only), the official .NET release metadata
  (`builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json`), GitHub Releases API, and pages on
  learn.microsoft.com / docs.github.com / semgrep docs. Nothing below is from memory unless explicitly marked
  **(background knowledge — not re-verified)**.
- Dev machine facts checked: Windows 11 22H2 (10.0.22621) x64; VS 2022 installed; `dotnet` hosts at
  `C:\Program Files\dotnet` (SDKs 8.0.424, 9.0.310, 9.0.317; runtimes up to 10.0.2 incl. WindowsDesktop 10.0.2)
  and `C:\Program Files (x86)\dotnet` (x86 host, **no SDKs**); node 22, PowerShell 7.6.4, winget 1.29, git 2.52.
- Related decisions already in the repo: `docs/adr/0001-stack-dotnet10-wpf.md` (Accepted 2026-08-16) chooses
  .NET 10 + WPF; `global.json` pins SDK `10.0.400` / `rollForward: latestPatch`. This report is consistent with
  that ADR and adds the WinUI 3 comparison the product owner asked for.

---

## Executive summary (pinned versions to adopt)

| Area | Pin | Why |
|---|---|---|
| .NET SDK | **10.0.400** (runtime 10.0.11), `global.json` `rollForward: latestPatch` | Latest LTS SDK band (released 2026-08-11). LTS until 2028-11-14. |
| TFMs | Core `net10.0`; Windows/CLI/App `net10.0-windows10.0.26100.0`, `SupportedOSPlatformVersion` 10.0.22000.0 (Win 11) | 26100 is the newest Windows SDK projection published for .NET (`Microsoft.Windows.SDK.NET.Ref 10.0.26100.87`); no 28000 projection exists yet. |
| GUI | **WPF** (ADR 0001) — alternative: WinUI 3 on **Windows App SDK 2.4.0** | See §B recommendation table. |
| WPF libs | WPF-UI 4.3.0 (optional), Microsoft.Xaml.Behaviors.Wpf 1.1.142 (optional) | Built-in Fluent theme in .NET 10 is "still in progress" per MS docs. |
| MVVM | CommunityToolkit.Mvvm **8.4.2** | Source-generated observable properties/commands; works for WPF and WinUI 3. |
| CLI | System.CommandLine **2.0.11** (+ Spectre.Console **0.57.2** for rich output) | 2.0 GA'd 2025-11-11 with .NET 10; monthly servicing; MIT. |
| DI/Logging/Config | Microsoft.Extensions.* **10.0.11**; Serilog **4.4.0**, Serilog.Extensions.Hosting 10.0.0, Serilog.Sinks.File 7.0.0, Serilog.Sinks.EventLog 4.0.0 | Same servicing train as the runtime. |
| Win32 interop | Microsoft.Windows.CsWin32 **0.3.298**; TaskScheduler **2.12.2**; System.Management **10.0.11**; System.ServiceProcess.ServiceController **10.0.11**; System.Diagnostics.EventLog **10.0.11**; Vanara.PInvoke.WUApi **5.0.7** (WUA COM) | See §D; do **not** use `<COMReference>` (unsupported by `dotnet build`). |
| JSON Schema | Corvus.Json.Validator **5.3.2** (AOT-friendly) or JsonSchema.Net **9.4.0** | See §D. |
| Tests | xunit.v3 **4.0.0** + xunit.runner.visualstudio 4.0.0 + xunit.analyzers 2.0.0 (MTP v2 = Microsoft.Testing.Platform 2.3.3), AwesomeAssertions **9.5.0**, NSubstitute **6.2.0**, Verify.XunitV3 **31.28.0**, FlaUI.UIA3 **5.0.0**, dotnet-stryker **4.16.0**, coverage via Microsoft.Testing.Extensions.CodeCoverage **18.10.0** or coverlet **10.0.1**, ReportGenerator **5.5.11** | xunit.v3 4.0.0 shipped 2026-08-15 (one day old) — fallback pin 3.2.2 if anything misbehaves. |
| Analyzers | Microsoft.CodeAnalysis.NetAnalyzers (in SDK, 10.0.400), Roslynator.Analyzers **4.16.1**, SonarAnalyzer.CSharp **10.32.0.713**, Meziantou.Analyzer **3.0.159**, Microsoft.VisualStudio.Threading.Analyzers **18.7.23**, Microsoft.CodeAnalysis.BannedApiAnalyzers / PublicApiAnalyzers **5.6.0**, AsyncFixer 2.1.0, IDisposableAnalyzers 4.0.8, xunit.analyzers 2.0.0, NSubstitute.Analyzers.CSharp 1.0.17 | StyleCop.Analyzers is stale (1.2.0-beta.556, 2023) — optional; SecurityCodeScan / Puma are dead (2022). |
| Packaging | WiX **7.0.0** (`wix` dotnet tool / WixToolset.Sdk 7.0.0; winget `WiXToolset.WiXCLI` 7.0.0.0) | MS-RL license (not MIT). |
| Repo tools | gitleaks 8.30.1, semgrep 1.173.0 (native Windows wheel), jscpd 5.0.15, markdownlint-cli2 0.23.2, cspell 10.0.1, editorconfig-checker 3.11.1, actionlint 1.7.12, PSScriptAnalyzer 1.25.0, pre-commit 4.6.2 / Husky.Net 0.9.1, ReferenceTrimmer 3.5.9, dotnet-outdated-tool 4.8.1, Microsoft.Sbom.DotNetTool 4.1.5 / CycloneDX 6.2.0, DotNet.ReproducibleBuilds 2.0.5 | |
| GitHub Actions | `windows-latest` = **windows-2025** (`windows-2025-vs2026` image), actions/checkout v7.0.1, actions/setup-dotnet **v6.0.0**, actions/cache v6.1.0, actions/upload-artifact v7.0.1, actions/download-artifact v8.0.1, actions/attest-build-provenance **v4.2.2** | Runner image has .NET SDK 10.0.302 max preinstalled → `setup-dotnet` with `global-json-file` is mandatory for 10.0.400. |

### Traps found (details in the sections below)

1. **PATH order on the dev machine is broken**: `dotnet` resolves to the x86 host (`C:\Program Files (x86)\dotnet`), which has no SDKs; `dotnet --list-sdks` prints nothing and `dotnet --version` fails with "No .NET SDKs were found". Fix the machine PATH (x64 first) — §A.3.
2. **Visual Studio 2022 cannot load SDK 10.0.400**: per Microsoft's SDK/VS matrix the minimum VS for SDK 10.0.200/300/400 is **18.0 (VS 2026)**; VS 17.14 supports only 10.0.1xx (and even then "targeting net10.0 is officially supported in VS 18.0+ only"). With `global.json` = 10.0.400 the solution will not open in VS 2022 → install VS 2026 (Community is free) or use VS Code / Rider — §A.4.
3. **`<COMReference>` (WUApiLib) does not build with `dotnet build`** — MSB4803 ("ResolveComReference is not supported on the .NET Core version of MSBuild"), dotnet/msbuild#3986 still open. Use Vanara.PInvoke.WUApi or hand-written `[GeneratedComInterface]` / `[ComImport]` interfaces — §D.4.
4. **`dotnet package list --vulnerable` always exits 0** (NuGet/Home#11315 closed "not planned" 2024-10-14). Gate on restore-time NuGetAudit with `WarningsAsErrors` NU1901–NU1904 instead — §F.4.
5. **Repo `NuGet.config` `auditSources` URL is wrong**: it points at `https://api.nuget.org/v3/vulnerabilities/index.json` (a resource), but audit sources must be a **service index** (`https://api.nuget.org/v3/index.json` or `https://data.nuget.org/v3/index.json`) or NU1905 fires — §F.4.
6. Windows App SDK **elevation support arrived in 1.1 (May 2022), not 1.0**, and requires OS servicing (KB5013943 / KB5013942); the 1.0-preview dynamic-dependency notes explicitly said "Elevated callers aren't supported" — §B.2.
7. **WiX is MS-RL, not MIT** — fine to build MSIs for an MIT product, but record it correctly — §G.1.
8. **FluentAssertions 8.x is under Xceed's non-commercial Community License** — use AwesomeAssertions (Apache-2.0 fork) — §E.2.
9. **Trusted Signing was renamed "Artifact Signing"**; individuals must be in the US or Canada; Basic $9.99/mo — §G.5.
10. Windows App SDK official `dotnet new` templates default to **WinAppSDK 1.8.260317003** and are `0.0.6-alpha` — bump to 2.4.0 explicitly — §B.5.
11. `windows-latest` moved to **windows-2025 + VS 2026 18.8** with .NET SDKs 8.0.423 / 9.0.316 / 10.0.302 preinstalled — pin `actions/setup-dotnet` + `global-json-file` — §F.7.

---

## A. .NET runtime / SDK

### A.1 Versions (source: `builds.dotnet.microsoft.com/dotnet/release-metadata/*/releases.json`, accessed 2026-08-16)

| Channel | Latest release | Date | Latest SDK (all bands in this release) | Support | EOL |
|---|---|---|---|---|---|
| **10.0 (LTS)** | **10.0.11** (security) | 2026-08-11 | **10.0.400** (VS 18.9), 10.0.303 (VS 18.8.3), 10.0.111 | active | **2028-11-14** |
| 11.0 (STS) | 11.0.0-**preview.7** | 2026-08-11 | 11.0.100-preview.7.26381.103 | preview | — (GA expected Nov 2026; RC1/RC2 normally Sep/Oct) |
| 9.0 (STS) | 9.0.19 | 2026-08-11 | 9.0.317 | maintenance | 2026-11-10 |
| 8.0 (LTS) | 8.0.30 | 2026-08-11 | 8.0.424 | maintenance | 2026-11-10 |

- C# language shipped with SDK 10.0.400: **C# 14** (`csharp-version` in the release metadata).
- Recommendation: **.NET 10 LTS**. .NET 11 is preview-only, STS, and would drag Windows App SDK / analyzers /
  toolkit packages onto preview versions (`11.0.0-preview.7` of every Microsoft.Extensions.* package exists but is
  not for production).
- winget IDs (verified with `winget show --exact --source winget`, 2026-08-16):
  - `Microsoft.DotNet.SDK.10` → **10.0.400**
  - `Microsoft.DotNet.DesktopRuntime.10` → **10.0.11**

### A.2 `global.json` semantics (source: learn.microsoft.com/dotnet/core/tools/global-json, ms.date 2026-03-05)

- `sdk.version` must be a full version (e.g. `10.0.400`); no wildcards or ranges.
- If `version` is set and `rollForward` is **omitted, the default is `patch`** ("uses the specified version; if not
  found, rolls forward to the latest patch level; if not found, fails").
- `latestPatch`: highest installed patch in the **same feature band** (10.0.4xx) ≥ requested. `latestFeature`: highest
  installed feature band + patch within 10.0 ≥ requested (10.0.400 → 10.0.5xx later). `feature` / `minor` / `major` roll
  to the *next* band only if the requested one is missing. `disable` = exact match (docs recommend `disable` when using
  package lock files so SDK and graph stay in lockstep — the repo already uses `RestorePackagesWithLockFile`).
- `allowPrerelease` default is `true` outside Visual Studio; the repo sets `false` (good).
- New in .NET 10 SDK: `sdk.paths` (local SDK search paths, `$host$`), `sdk.errorMessage`, and `test.runner`
  (`"Microsoft.Testing.Platform"`) — the last one is how `dotnet test` switches to MTP mode (see §E.4).
- Recommendation for this repo: keep `10.0.400` + `latestPatch` (+ `allowPrerelease: false`). Consider
  `rollForward: disable` only if you also commit `packages.lock.json` and want the exact-SDK guarantee; `latestPatch`
  is the pragmatic default because monthly SDK patches are security releases.

### A.3 Dev-machine PATH problem (verified 2026-08-16)

```text
$ echo $PATH | tr ':' '\n' | grep -i dotnet
/c/Program Files (x86)/dotnet      <-- first
/c/Program Files/dotnet
$ dotnet --list-sdks                 <-- x86 host: prints nothing
$ dotnet --version                   <-- "No .NET SDKs were found."
$ "/c/Program Files/dotnet/dotnet.exe" --list-sdks
8.0.424 / 9.0.310 / 9.0.317
```

Fix (elevated PowerShell, then reopen terminals; VS / VS Code pick up the machine PATH on restart):

```powershell
$m = [Environment]::GetEnvironmentVariable('Path','Machine') -split ';' | Where-Object { $_ }
$m = @('C:\Program Files\dotnet') + ($m | Where-Object { $_ -ne 'C:\Program Files\dotnet' -and $_ -ne 'C:\Program Files (x86)\dotnet' })
[Environment]::SetEnvironmentVariable('Path', ($m -join ';'), 'Machine')
[Environment]::SetEnvironmentVariable('DOTNET_ROOT', 'C:\Program Files\dotnet', 'Machine')
```

Then `winget install --id Microsoft.DotNet.SDK.10 --exact` (installs 10.0.400 into `C:\Program Files\dotnet`).
Keep the x86 runtime folder itself (some x86 apps need it) — only its PATH precedence is harmful. If you cannot
change PATH, invoke `C:\Program Files\dotnet\dotnet.exe` explicitly (and set `DOTNET_ROOT`) in scripts.

### A.4 SDK feature band vs Visual Studio (source: learn.microsoft.com/dotnet/core/porting/versioning-sdk-msbuild-vs, ms.date 2026-06-08)

| SDK | Ships with VS | **Minimum VS** | Notes |
|---|---|---|---|
| 10.0.100 (1xx) | 18.0 | **17.14** | "Targeting net10.0 is officially supported in Visual Studio 18.0+ only" — 17.14 builds it with a warning. |
| 10.0.200 | 18.4 | **18.0** | |
| 10.0.300 | 18.6 | **18.0** | |
| **10.0.400** | **18.9** | **18.0** | Ship Aug '26; "final feature bands ... are supported for the life of the matching runtime". |

Consequence: with `global.json` = `10.0.400`, **VS 2022 (17.14) will not load the projects** (the page: "The SDK has a
minimum version of MSBuild and Visual Studio that it works with, and it won't load in a version of Visual Studio that's
older than that minimum version"). Options: (a) install **Visual Studio 2026** (18.9) — recommended, WinUI/WPF tooling
lives there; (b) work in VS Code (C# Dev Kit) / Rider with the CLI; (c) temporarily pin `10.0.111` (`latestPatch`) for
VS 2022, accepting the "not officially supported" warning. `dotnet build` from the CLI is unaffected either way.

---

## B. UI framework — Windows App SDK / WinUI 3 vs WPF

### B.1 Windows App SDK versions (sources: NuGet registration API; learn.microsoft.com/windows/apps/windows-app-sdk/release-channels, updated 2026-08-13; release-notes/windows-app-sdk-2-0; release-notes/windows-app-sdk-1-8)

| Package | Latest stable | Published | Notes |
|---|---|---|---|
| Microsoft.WindowsAppSDK (metapackage) | **2.4.0** | 2026-08-13 | Depends on: Base 2.0.4, Foundation 2.3.9, InteractiveExperiences 2.1.6, **WinUI 2.3.6**, DWrite 2.1.0, Widgets 2.0.5, AI 2.4.4, ML 2.1.74, Search 2.4.4, **Runtime [2.4.0]** (exact). |
| Microsoft.WindowsAppSDK.WinUI | 2.3.6 | 2026-08-13 | Deps: Microsoft.Web.WebView2 ≥1.0.3719.77, Base ≥2.0.4, Foundation ≥2.3.9, InteractiveExperiences ≥2.1.3. |
| Microsoft.WindowsAppSDK.Foundation | 2.3.9 | 2026-08-13 | Deps: Base ≥2.0.4, InteractiveExperiences ≥2.1.3. |
| Microsoft.WindowsAppSDK.Base | 2.0.4 | 2026-05-21 | Deps: Microsoft.Windows.SDK.BuildTools ≥10.0.26100.4654, Microsoft.Windows.SDK.BuildTools.MSIX ≥1.7.251221100. |
| Microsoft.WindowsAppSDK.Runtime | 2.4.0 | 2026-08-13 | Reference this to get **framework-dependent** deployment; without it component packages behave as `WindowsAppSDKSelfContained=true` (1.8.0 release notes). |
| 1.8 line | 1.8.260804001 (= 1.8.11) | 2026-08-13 | Maintenance until **2026-09-09**; then out of support. |
| 1.7 line | 1.7.260224002 | 2026-03-10 | **Out of support since 2026-03-18.** |
| Templates: Microsoft.WindowsAppSDK.WinUI.CSharp.Templates | 0.0.6-alpha (prerelease only) | 2026-06-01 | Official `dotnet new` templates (see B.5). |
| Microsoft.Windows.SDK.BuildTools.WinApp | 0.6.0 | 2026-08-12 | Adds `dotnet run` for **packaged** WinUI apps (registers debug identity). |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.8249 (26100 line) / 10.0.28000.2526 (26H1 line) | 2026-05-26 / 2026-07-24 | NuGet-delivered makeappx / signtool / mt etc. |
| Microsoft.Windows.SDK.NET.Ref | 10.0.26100.87 | 2026-07-23 | WinRT projection used by `net10.0-windows10.0.26100.0`; **no 10.0.28000.x published** → 26100 is the max TFM. |
| Microsoft.Windows.CsWinRT | 2.3.1 (3.0.0-preview.260319.2 exists) | 2026-07-22 | Only needed explicitly for authoring / AOT source generators; the SDK bundles the projection. |
| WinUIEx | 2.9.3 | 2026-08-13 | MIT. Depends on Microsoft.WindowsAppSDK.WinUI ≥1.8.250906003 (open-ended → restores with 2.x). Window management, persistence, tray icon, splash, `WindowManager`. |
| CommunityToolkit.WinUI.* (Controls.SettingsControls, Controls.Segmented, Extensions, Behaviors, Converters, Animations, Triggers, Helpers, …) | **8.2.251219** | 2025-12-20 | TFMs `net8.0-windows10.0.19041`, `net9.0-windows10.0.19041` (no net10 asset; the net9 asset is used on net10). Depends on **Microsoft.WindowsAppSDK ≥1.6.250108002** (open-ended). Newer prerelease 8.3.260402-preview2 (2026-04-02) depends on Microsoft.WindowsAppSDK.WinUI ≥1.8.260204000. No explicit "2.x supported" statement in the toolkit repo as of today — do a smoke test. |
| CommunityToolkit.Mvvm | **8.4.2** | 2026-03-25 | TFMs netstandard2.0/2.1, net8.0, net8.0-windows10.0.17763. |
| Windows App Runtime (winget) | `Microsoft.WindowsAppRuntime.2` → 2.2.0; `Microsoft.WindowsAppRuntime.2.0` → 2.0.1; `Microsoft.WindowsAppRuntime.1.8` → 1.8.9 | (winget show 2026-08-16) | Because 2.x aligns the package family name to the **major** version, one "2" runtime serves 2.0–2.4 (2.0 release notes: "the next side-by-side release ... will be version 3.0.0"). |

Windows App SDK 2.x facts (release notes, stable pivot):

- 2.0.1 = first release on **SemVer 2.0.0** ("aligning the Windows App SDK version with the NuGet package version").
  Stable cadence since: 2.1.3 (2026-05-21), 2.2.0 (2026-06-09), 2.3.1 (2026-07-16), 2.4.0 (2026-08-13). 2.0 lifecycle
  row: "Current", end of servicing 2027-04-29.
- 2.2.0 added `Microsoft.Windows.Storage.ApplicationData.GetForUnpackaged()`; 2.3.1 changed `DISABLE_XAML_GENERATED_MAIN`
  semantics (renames instead of removes the generated `Main`) and added `XamlOptionalChanges`; 2.4.0 adds haptics /
  touchpad input. No breaking WinUI control API changes are documented in the 2.x stable notes.
- The command-line quickstart (learn.microsoft.com/windows/apps/get-started/start-here, ms.date 2026-07-21) lists as
  prerequisites only: Windows 10 1809+, Developer Mode, **.NET 10 SDK or later**; the Visual Studio path says
  **Visual Studio 2026** with the "WinUI application development" workload (`winget configure -f https://aka.ms/winui-config`).
- Minimum OS: "the Windows App SDK is currently backward compatible to Windows 10 version 1809" (release-channels page);
  templates set `TargetPlatformMinVersion` 10.0.17763.0. This app is Windows-11-only, so set
  `SupportedOSPlatformVersion` / `TargetPlatformMinVersion` = 10.0.22000.0.

### B.2 Elevation (running as administrator)

- **1.1 stable (2022-05-24)** — "Elevation: Apps are now able to run with elevated privilege." Known limitations:
  "Elevated support requires the following OS servicing update: Win11 – May 10, 2022 KB5013943 (OS Build 22000.675);
  Win10 – May 10, 2022 KB5013942 (OS Builds 19042.1706, 19043.1706, and 19044.1706)"; "App and Push Notifications for an
  elevated unpackaged app is not supported"; (1.1 preview: "Elevated WinUI apps crash when dragging an element during a
  drag-and-drop interaction" — fixed later). Windows 11 22H2 (22621) already satisfies the OS requirement.
- 1.0 (Nov 2021) did **not** support it — the 1.0-preview notes for dynamic dependencies say "Elevated callers aren't
  supported." So: "unpackaged elevated since **1.1**", not 1.0.
- 1.8.0 (2025-09-09) added `Microsoft.Windows.Storage.Pickers` specifically because "the existing Windows.Storage.Pickers
  APIs do not work when the application is running as an administrator". Use the new pickers in an elevated app.
- Framework-dependent unpackaged apps: the Windows App Runtime installer, when "running elevated and the user doing the
  installation has admin privileges", registers the MSIX framework packages **system-wide** (`ProvisionPackageForAllUsersAsync`);
  otherwise per-user (deploy-unpackaged-apps page). For an elevated utility this is convenient, but self-contained avoids
  the runtime dependency entirely (recommended for a portable EXE / MSI; see B.4).
- MSIX / packaged apps + admin **(background knowledge — not re-verified this session)**: an MSIX manifest cannot declare
  `requireAdministrator`; the app must self-elevate (relaunch with `runas`) and declare the restricted capability
  `allowElevation`. Keep the elevated code path identical for packaged and unpackaged builds and elevate at process start.

### B.3 Native AOT / trimming (source: release-notes/windows-app-sdk-1-6)

- "Native AOT support: The .NET PublishAot project property is now supported" since **1.6.0 (2024-09-04)**; guidance:
  set `<PublishAot>true</PublishAot>` unconditionally; use `{x:Bind}` (reflection `{Binding}` targets need rooting via
  `TrimmerRootDescriptor`); mark WinRT-exposed classes `partial`; C#/WinRT ≥ 2.1.1 (now bundled). Known issues at the
  time: WebView2 projection not AOT-compatible (fixed in later WebView2 packages), and a GC race after page navigation
  (dotnet/runtime#104582). Official templates default to `PublishTrimmed=true` + `PublishReadyToRun=true` for non-Debug.
- Practical stance for this app: AOT the **CLI** (pure .NET + CsWin32) if desired; do not AOT the GUI in v1
  (WPF cannot; WinUI 3 can but every dependency — toolkit, WinUIEx, System.Management, WMI/COM — must be trim-safe, and
  System.Management / classic COM interop is not).

### B.4 Deployment modes for WinUI 3 (source: deploy-unpackaged-apps, 1.8.0 release notes, template csproj)

- Unpackaged: `<WindowsPackageType>None</WindowsPackageType>` (auto-initializer calls the Bootstrapper); framework-dependent
  needs the Windows App Runtime installed (per-user or machine-wide) — ship `WindowsAppRuntimeInstall-x64.exe` or use
  the winget dependency; self-contained: `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` (or simply omit
  `Microsoft.WindowsAppSDK.Runtime` when referencing component packages) copies the runtime next to the EXE.
- Publish: `dotnet publish src/App -c Release -r win-x64 -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true --self-contained`
  (single-file publish is **not** supported for WinUI 3 apps — background knowledge, not re-verified; keep a folder
  publish / MSI).
- Templates default `EnableMsixTooling=true` and `Platforms=x86;x64;ARM64`; keep `RuntimeIdentifier` = `win-x64` for the
  unpackaged build and enable `EnableMsixTooling` only in the packaging configuration to reduce CI friction.

### B.5 Creating a WinUI 3 project without Visual Studio (verified against the template package contents)

```powershell
dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates      # 0.0.6-alpha as of 2026-08-16 (append ::0.0.6-alpha if no version is picked)
dotnet new winui -n OpenTheWindows.App     # aliases: winui3, wasdk-single; other templates: winui.classlib, mvvmapp, navview, tabview, unittest, item templates
```

Template facts: `TargetFramework = $DotNetVersion$-windows10.0.26100.0` (choices net8.0 / net9.0 / **net10.0** default),
`TargetPlatformMinVersion` 10.0.17763.0, `UseWinUI=true`, `EnableMsixTooling=true`, `WinUISDKReferences=false`,
`Nullable=enable`, `PublishTrimmed=true` in Release; package defaults **Microsoft.WindowsAppSDK 1.8.260317003**,
Microsoft.Windows.SDK.BuildTools 10.0.26100.7705, Microsoft.Windows.SDK.BuildTools.WinApp 0.3.1 → **override to 2.4.0 /
10.0.26100.8249 / 0.6.0** in `Directory.Packages.props`. The `winui.unittest` template is MSTest-based and runs inside a
WinUI app host (`Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer`, `[UITestMethod]`) — this is the only
supported way to unit-test WinUI 3 UI code, and it is not friendly to `dotnet test` / CI (see §H).

Build prerequisites for **C#** WinUI 3: .NET 10 SDK only (Windows SDK build tools arrive as NuGet packages via
Microsoft.WindowsAppSDK.Base → Microsoft.Windows.SDK.BuildTools). A separate Windows SDK / VS Build Tools install is
required only for C++/WinRT projects. The `windows-latest` runner has Windows SDK 10.0.26100 and VS 2026 anyway.

### B.6 WPF on .NET 10 (source: learn.microsoft.com/dotnet/desktop/wpf/whats-new/net100, ms.date 2026-02-10)

- "Fluent UI style support is still in progress." .NET 10 added Fluent styles for DatePicker, GridSplitter, GridView,
  GroupBox, Hyperlink, Label, NavigationWindow, RichTextBox, TextBox; fixed the Expander animation and HighContrast crashes.
  The page does not announce `ThemeMode` leaving experimental status — in .NET 9 the API carried
  `[Experimental("WPF0001")]` **(background knowledge — re-check on first use; if the diagnostic still fires, add
  `<NoWarn>$(NoWarn);WPF0001</NoWarn>` for the App project only)**.
- Other .NET 10 WPF changes: unified WPF / WinForms clipboard API (BinaryFormatter-based clipboard methods obsoleted),
  `Grid ColumnDefinitions="1*,Auto"` short syntax, extra `MessageBoxButton` / `MessageBoxResult` values, perf work.
- Trimming / Native AOT: WPF is **not** trim/AOT compatible (ADR 0001 records this; background knowledge). Ship
  framework-dependent (`Microsoft.DotNet.DesktopRuntime.10`) or self-contained.
- Third-party: **WPF-UI 4.3.0** (2026-05-04, MIT, TFMs incl. `net10.0-windows7.0`; Fluent / Mica controls, NavigationView,
  SnackBar), WPF-UI.Tray 4.3.0; **Microsoft.Xaml.Behaviors.Wpf 1.1.142** (2026-03-02).
- UI automation: WPF exposes UIA natively; FlaUI 5.0.0 (UIA3) drives it well.

### B.7 Recommendation: WinUI 3 vs WPF for *this* app

| Criterion | WinUI 3 (Windows App SDK 2.4.0) | WPF (.NET 10) |
|---|---|---|
| Native Win 11 look | Best (Fluent 2, Mica, `TitleBar`, SettingsCard via toolkit) | Good with .NET 10 Fluent theme (incomplete) or WPF-UI; not pixel-identical |
| Elevated (admin) unpackaged | Supported since 1.1; needs new `Microsoft.Windows.Storage.Pickers`; some APIs (notifications) unsupported when elevated | Fully supported; nothing special |
| Portable EXE / MSI without runtime install | Only with `WindowsAppSDKSelfContained=true` (larger output); framework-dependent needs the Windows App Runtime install (elevated installer provisions machine-wide) | Framework-dependent needs .NET Desktop Runtime 10 (already on the dev machine; common), or self-contained |
| MSIX / Intune later | First-class (`EnableMsixTooling`) | Possible via a Windows Application Packaging project / MSIX Packaging Tool; more manual |
| Designer / tooling | No XAML designer; Hot Reload works; **best experience requires VS 2026** workload; CLI-only path works but templates are 0.0.6-alpha | Mature designer, Live Visual Tree, XAML Hot Reload |
| MVVM | CommunityToolkit.Mvvm 8.4.2, `{x:Bind}` compile-time binding | CommunityToolkit.Mvvm 8.4.2, `{Binding}` (reflection) |
| Unit-testing the UI layer | Only via the WinUI test host app (MSTest, `[UITestMethod]`), awkward under `dotnet test`; keep ViewModels UI-agnostic | Plain xunit test project can instantiate WPF types on an STA thread; FlaUI for E2E |
| CI friction (GitHub `windows-latest`) | Higher: XAML compiler (MSB3073 error hiding under `dotnet build` was fixed only in 1.8.8), `TreatWarningsAsErrors` collides with generated `XamlTypeInfo`, `Platforms` matrix, packaging steps | Low: `dotnet build` / `dotnet test` just work |
| Footprint / lifecycle | Larger; separate WinAppSDK runtime lifecycle (2.x line supported ~1 year; 1.8 EOL 2026-09-09) | Framework only; .NET 10 LTS to 2028 |
| Trimming / AOT | Possible (1.6+) but every dependency must be trim-safe | Not possible |
| Analyzer strictness (`latest-all`, warnings-as-errors) | Generated code needs `NoWarn`s / `.globalconfig` for `*.g.cs`, `XamlTypeInfo.g.cs` | Generated `*.g.cs` / `*.g.i.cs` also need a couple of `NoWarn`s but far fewer |

**Recommendation:** WPF (as ADR 0001 already records) — the app is an elevated system utility whose value is in Core /
Windows logic, and WPF minimises CI / tooling friction, keeps unsigned portable / MSI distribution simple, and is trivially
UIA-testable. Choose WinUI 3 only if a native Windows 11 look is a hard product requirement; if so pin **Windows App
SDK 2.4.0 + WinUIEx 2.9.3 + CommunityToolkit.WinUI 8.2.251219**, build self-contained + unpackaged, and keep the GUI a
thin shell over the same ViewModels so the decision stays reversible.

---

## C. CLI framework

| Package | Latest stable | Published | Source | Notes |
|---|---|---|---|---|
| System.CommandLine | **2.0.11** | 2026-08-11 | nuget.org registration API; github.com/dotnet/command-line-api (MIT) | 2.0.0 GA 2025-11-11 (with .NET 10); monthly servicing 2.0.1 … 2.0.11; TFMs net8.0 + netstandard2.0. **3.0.0-preview.7.26381.103** (2026-08-11) tracks .NET 11 — ignore. |
| System.CommandLine.Hosting | 0.4.0-alpha.25306.1 (no stable) | 2025-06-19 | nuget | Do not depend on it; wire Microsoft.Extensions.Hosting manually. |
| Spectre.Console | 0.57.2 | 2026-07-02 | nuget | Rich tables / trees / progress; TFMs incl. net10.0. |
| Spectre.Console.Cli | 0.55.0 | 2026-04-03 | nuget | Alternative full CLI framework. |
| CliFx | 3.0.0 | 2026-04-03 | nuget / Tyrrrz/CliFx | Alternative. |
| ConsoleAppFramework | 5.7.13 | 2025-11-26 | nuget / Cysharp | Source-generated, zero-reflection, AOT-friendly alternative. |
| Cocona | 2.2.0 | 2023-03-27 | nuget | Stale — avoid. |

**Recommendation:** System.CommandLine 2.0.11 — Microsoft-maintained, GA, MIT, trim/AOT-friendly, POSIX + Windows
conventions, response files, tab-completion, and it is what `dotnet` itself uses. Use Spectre.Console 0.57.2 for
rendering only (tables, progress); do not mix in Spectre.Console.Cli. Enterprise-relevant details to design in from
day one: `--output json`, documented exit codes, `--non-interactive`, `--dry-run`, and an `otw.exe` that refuses to run
without elevation unless it is a read-only command (`scan`, `export`, `--whatif`).

---

## D. Windows interop libraries

### D.1 Table (nuget.org registration API, 2026-08-16)

| Package | Latest | Published | TFMs (highest) | Notes / compat with net10.0 |
|---|---|---|---|---|
| Microsoft.Win32.Registry | (in-box) `Microsoft.Win32.Registry.dll` ships in `Microsoft.NETCore.App` 10.0 | — | — | The NuGet package 5.0.0 (2020) is only for netstandard2.0 libraries; do not reference it. Verified: `shared\Microsoft.NETCore.App\10.0.2\Microsoft.Win32.Registry.dll` and `System.Security.Principal.Windows.dll` present. |
| Microsoft.Windows.CsWin32 | **0.3.298** | 2026-06-17 | analyzer (netstandard2.0) | MIT. Source-generated P/Invoke from Win32 metadata (`NativeMethods.txt`). Use for `SRSetRestorePoint`, `RtlGetVersion`, `NetGetJoinInformation`, `IsDeviceRegisteredWithManagement`, `IGroupPolicyObject`, `RefreshPolicyEx`, `ShellExecuteEx`, etc. AOT/trim safe (`LibraryImport`). |
| TaskScheduler (dahall) | **2.12.2** | 2025-07-08 | net9.0-windows7.0 (also net8 / net6 / netstandard2.0) | MIT. Wraps Task Scheduler 2.0 COM. No net10 asset — the net9.0-windows asset resolves fine on net10.0-windows. Repo active (pushed 2026-06-21). Not trim/AOT safe (classic COM interop). |
| Vanara.PInvoke.TaskSchd | 5.0.7 | 2026-08-15 | net10.0-windows7.0 | Lower-level alternative (MIT). |
| System.ServiceProcess.ServiceController | **10.0.11** | 2026-08-11 | net10.0 | Start / stop / query services; changing start mode is not in the API → `ChangeServiceConfig` via CsWin32 (or the registry `Start` value with care). |
| System.Management (WMI) | **10.0.11** | 2026-08-11 | net10.0 | Microsoft-serviced monthly with the runtime; simplest for `Win32_*`, `root\Microsoft\Windows\Defender` (`MSFT_MpPreference`, `MSFT_MpComputerStatus`), `SystemRestore`. Not trim/AOT safe. |
| Microsoft.Management.Infrastructure (MMI / CIM) | 3.0.0 | 2023-11-03 | (runtime.win) | What PowerShell's CIM cmdlets use; supports CIM sessions / WS-Man; last release 2023. Fine, but heavier and rarely updated. |
| Microsoft.Windows.SDK.NET.Ref (WinRT projection) | 10.0.26100.87 | 2026-07-23 | (bundled with SDK) | `Windows.Management.Deployment.PackageManager` (Appx removal / deprovisioning; `RemovePackageAsync(..., RemovalOptions.RemoveForAllUsers)` and `DeprovisionPackageForAllUsersAsync` need admin), `Windows.Security.Credentials`, `Windows.System.Profile.AnalyticsInfo`, etc. Requires TFM `net10.0-windows10.0.26100.0`; annotate with `[SupportedOSPlatform("windows10.0.22000")]` as needed. |
| Vanara.PInvoke.WUApi | **5.0.7** | 2026-08-15 | net10.0-windows7.0 | MIT. Hand-written Windows Update Agent COM interop (`IUpdateSession`, `IUpdateSearcher`, `IUpdateInstaller`, `IAutomaticUpdates2`, `IUpdateServiceManager2`). See D.4. |
| Microsoft.Windows.Compatibility | 10.0.11 | 2026-08-11 | net10.0 | Meta-package (pulls System.Management, EventLog, ServiceController, DirectoryServices, …). Prefer referencing the individual packages. |
| System.Diagnostics.EventLog | **10.0.11** | 2026-08-11 | net10.0 | Write to the Application log / a custom log; creating an event **source** requires admin (fine here). `EventLogTraceListener` lives here too. |
| Microsoft.Extensions.Logging.EventLog | 10.0.11 | 2026-08-11 | net10.0 | `AddEventLog()` provider for M.E.Logging. |
| Serilog | **4.4.0** | 2026-07-10 | net10.0 | Apache-2.0. |
| Serilog.Sinks.EventLog | 4.0.0 (5.0.0-dev prerelease) | 2024-06-14 | net8.0 (uses System.Diagnostics.EventLog 8.0.0 → transitively upgraded to 10.0.11 by CPM) | OK on net10. |
| Serilog.Sinks.File / .Console | 7.0.0 / 6.1.1 | 2025-04-28 / 2025-11-02 | net9.0 / net8.0 | |
| Serilog.Extensions.Hosting / .Logging / .Settings.Configuration | 10.0.0 / 10.0.0 / 10.0.1 | 2025-11-28 / 2025-11-21 / 2026-06-15 | net10.0 | Match Microsoft.Extensions 10.x. |
| Microsoft.Extensions.Hosting / DependencyInjection / Logging / Configuration(.Json / .Binder / .CommandLine / .EnvironmentVariables) / Options(.DataAnnotations) / FileSystemGlobbing | **10.0.11** | 2026-08-11 | net10.0 | 11.0.0-preview.7 exists — ignore. |
| Microsoft.Extensions.TimeProvider.Testing / Diagnostics.Testing | 10.9.0 | 2026-08-11 | net10.0 | Fake `TimeProvider` and `FakeLogger` for tests. |
| System.Text.Json | (in-box; package 10.0.11) | 2026-08-11 | — | Use a source-generated `JsonSerializerContext` for the catalog (trim-safe). |
| Corvus.Json.Validator / Corvus.Json.ExtendedTypes / Corvus.Json.SourceGenerator | **5.3.2** | 2026-08-10 | net10.0 | JSON Schema 2020-12 / 2019-09 / draft-7 validation over `System.Text.Json`; **source-generated, AOT/trim friendly**; Apache-2.0. Recommended for validating `catalog/*.json` at build / test time and at runtime. |
| JsonSchema.Net | 9.4.0 | 2026-07-26 | net10.0 | Mature alternative (MIT); easy API. Either is fine; Corvus if AOT matters, JsonSchema.Net for simplicity. |
| NJsonSchema | 11.6.1 | 2026-04-20 | net8.0 | Schema *generation* from C# types (Newtonsoft-based); use only to emit `catalog.schema.json` at build time, if at all. |
| YamlDotNet | 18.1.0 | 2026-06-26 | net10.0 | Not needed (JSON catalog per ADR 0004). |
| Polly / Polly.Core | 8.7.0 | 2026-06-10 | net8.0 | Not needed; `Microsoft.Extensions.Http.Resilience` 10.9.0 exists if HTTP retry is ever required. |
| Microsoft.PowerShell.SDK / System.Management.Automation | 7.6.5 | 2026-08-14 | net10.0 | Only if hosting PowerShell in-proc (heavy) — prefer not. |
| System.IO.Abstractions (+ TestingHelpers) | 22.2.0 | 2026-07-12 | net10.0 | Optional file-system abstraction; the ADR's own abstractions are enough. |
| Microsoft.Windows.ImplementationLibrary (WIL) | 1.0.260126.7 | 2026-01-26 | native | C++ only — not applicable. |

### D.2 Elevation check / manifest

- In-box: `new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)`
  (`System.Security.Principal.Windows.dll` is in the shared framework — verified). Also check the token elevation type
  via `GetTokenInformation(TokenElevationType)` (CsWin32) if you must distinguish "admin but not elevated (UAC)".
- Ship `app.manifest` with `<requestedExecutionLevel level="requireAdministrator" uiAccess="false"/>` for the GUI and
  the CLI (`<ApplicationManifest>` in csproj — the WinUI templates already carry one; add `supportedOS` GUIDs for
  Windows 10/11 and `dpiAwareness` PerMonitorV2 for WPF).

### D.3 Windows edition / version / MDM detection (all in-box or CsWin32)

- Version: `RtlGetVersion` (ntdll, CsWin32) or `Environment.OSVersion` (accurate since .NET 5 — no compat shim) plus
  registry `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion` (`DisplayVersion`, `CurrentBuild`, `UBR`, `EditionID`,
  `ProductName`); `Win32_OperatingSystem` for `Caption` / `OperatingSystemSKU`. `Windows.System.Profile.AnalyticsInfo`
  and `ApiInformation.IsApiContractPresent` are available via the WinRT projection.
- Domain / MDM: `NetGetJoinInformation` (lmjoin.h) → workgroup vs domain; `IsDeviceRegisteredWithManagement` /
  `GetDeviceManagementConfigInfo` (mdmregistration.h; some calls need elevation) → MDM enrolment;
  Entra join state: `dsregcmd /status` output (no public API; the docs page describes the fields) or the registry keys
  under `HKLM\SYSTEM\CurrentControlSet\Control\CloudDomainJoin\JoinInfo`; MDM policy presence:
  `HKLM\SOFTWARE\Microsoft\PolicyManager\current\device\*` and `HKLM\SOFTWARE\Microsoft\Enrollments\*`.
  Sources: learn.microsoft.com/windows/win32/api/lmjoin/nf-lmjoin-netgetjoininformation;
  learn.microsoft.com/windows/win32/api/mdmregistration/nf-mdmregistration-isdeviceregisteredwithmanagement;
  learn.microsoft.com/entra/identity/devices/troubleshoot-device-dsregcmd.

### D.4 Windows Update Agent API (WUApiLib) — how to reference

- **Do not use `<COMReference Include="WUApiLib">`**: `dotnet build` (the .NET Core MSBuild) cannot run
  `ResolveComReference` — error **MSB4803**; only `msbuild.exe` from Visual Studio can. Tracking issue
  dotnet/msbuild#3986 is still **open** (comments as recent as 2025-12-12 report the same failure). It would break
  GitHub Actions and every non-VS build.
- Options, in order of preference:
  1. **Vanara.PInvoke.WUApi 5.0.7** (MIT, net10.0-windows7.0 asset, released 2026-08-15) — complete, hand-written interop for
     the WUA COM object model; no tlbimp step. Not trim/AOT-safe (classic COM), which is fine for the Windows project.
  2. Hand-write only the interfaces you need with `[ComImport]` / `[Guid]` (or the .NET 8+ `[GeneratedComInterface]` for
     AOT-safety) and activate `Microsoft.Update.Session` / `Microsoft.Update.AutoUpdate` / `Microsoft.Update.ServiceManager`
     via `Type.GetTypeFromProgID` / `CoCreateInstance`. Small surface (search, download, install, `IAutomaticUpdatesSettings2`,
     `IUpdateServiceManager2` for adding the Microsoft Update service).
  3. Late-bound `dynamic` (needs `Microsoft.CSharp`, no compile-time checking, not AOT) — avoid.
- No Microsoft-maintained managed wrapper exists on NuGet (searched `Interop.WUApiLib`, `WUApiLib` — 404).

### D.5 Group Policy / `registry.pol`

- **LGPO.exe** (Microsoft Security Compliance Toolkit) — imports / exports `Registry.pol`, security templates and
  "LGPO text". It is distributed under Microsoft's software license terms for the toolkit (**not** an OSS license; no
  redistribution rights are granted) → do **not** bundle it; at most detect it on PATH as an optional accelerator.
  Source: learn.microsoft.com/windows/security/operating-system-security/device-management/windows-security-configuration-framework/security-compliance-toolkit-10.
- **PolicyPlus** (Fleex255/PolicyPlus): GitHub reports license **CC-BY-4.0**; snapshot release "October2025"
  (2025-10-23), last push 2025-12-27; it is a Windows Forms app (not a NuGet library). Useful as a reference implementation
  of ADMX parsing and `registry.pol` writing, not as a dependency.
- No maintained .NET NuGet library wraps `IGroupPolicyObject`. Recommended approach: implement a small
  `RegistryPolicyFile` reader / writer for the documented **"PReg" Registry Policy File Format** (header `PReg` + version 1,
  then `[key;value;type;size;data]` UTF-16LE records; source:
  learn.microsoft.com/previous-versions/windows/desktop/policy/registry-policy-file-format), write
  `%SystemRoot%\System32\GroupPolicy\{Machine|User}\Registry.pol`, update `gpt.ini`, and call
  `IGroupPolicyObject.OpenLocalMachineGPO` / `Save` (gpedit.h; `CLSID_GroupPolicyObject`; source:
  learn.microsoft.com/windows/win32/api/gpedit/nn-gpedit-igrouppolicyobject) followed by `RefreshPolicyEx` — all
  reachable through CsWin32 (`IGroupPolicyObject` and `RefreshPolicyEx` are in the Win32 metadata).

### D.6 System Restore

- Native: `SRSetRestorePointW` from `srclient.dll` (CsWin32-generatable; structs `RESTOREPOINTINFOW`,
  `STATEMGRSTATUS`). WMI alternative: `root\default:SystemRestore.CreateRestorePoint("desc", 12, 100)` via
  System.Management (this is what `Checkpoint-Computer` uses).
- Behaviour to design for **(background knowledge — verify on the target OS)**: Windows creates at most one restore point
  per 24 h by default (`HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore\SystemRestorePointCreationFrequency`,
  minutes; 0 disables the throttle), System Protection may be disabled on the system drive, and the call needs elevation.
  Always fall back to the app's own journal (ADR 0004 / research 07).

---

## E. Testing stack

### E.1 Frameworks (nuget.org, 2026-08-16)

| Package | Latest | Published | Notes |
|---|---|---|---|
| **xunit.v3** | **4.0.0** | 2026-08-15 | Depends on `xunit.v3.mtp-v2 [4.0.0]` → **Microsoft.Testing.Platform v2 (2.3.3) is the default**; release notes (xunit.net/releases/v3/4.0.0, 2026-08-14): "we are discontinuing official support for Microsoft Testing Platform v1", Mono support dropped, Native AOT support added, test class / method orderers, full parallelization with opt-out. Console runner is now a .NET tool needing ".NET 10 SDK or later". Fallback if 4.0.0 is too fresh: 3.2.2 (2026-01-14). |
| xunit.runner.visualstudio | 4.0.0 | 2026-08-15 | VSTest adapter (Test Explorer / `dotnet test` in VSTest mode). |
| xunit.analyzers | 2.0.0 | 2026-08-15 | |
| xunit.v3.templates | 4.0.0 | 2026-08-15 | `dotnet new install xunit.v3.templates` → `dotnet new xunit3`. |
| Microsoft.Testing.Platform | 2.3.3 | 2026-07-28 | Also Microsoft.Testing.Extensions.TrxReport / HangDump / CrashDump / Retry 2.3.3. |
| Microsoft.Testing.Extensions.CodeCoverage | 18.10.0 | 2026-08-12 | `--coverage --coverage-output-format cobertura` in MTP mode. |
| Microsoft.NET.Test.Sdk / Microsoft.TestPlatform.TestHost | 18.9.0 | 2026-08-14 | Needed only for VSTest mode. |
| xunit (v2) | 2.9.3 | 2025-01-08 | Legacy; not recommended for a new repo. |
| MSTest / MSTest.TestFramework | 4.3.3 | 2026-07-28 | Solid; the WinUI `unittest` template uses it. |
| TUnit | 1.65.0 | 2026-08-13 | Source-generated, MTP-native, AOT; very fast-moving. |
| NUnit | 4.6.1 (5.0.0-beta.1) | 2026-05-19 | |

**Recommendation:** xunit.v3 4.0.0 in **MTP mode** (`global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`,
`<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` in test projects, no Microsoft.NET.Test.Sdk).
Keep xunit.runner.visualstudio for VS Test Explorer. If MTP mode causes friction on day one, run VSTest mode with
Microsoft.NET.Test.Sdk 18.9.0 + coverlet.collector 10.0.1 — both are supported.

### E.2 Assertions and mocking

| Package | Latest | Published | License / note |
|---|---|---|---|
| FluentAssertions | 8.10.0 | 2026-05-12 | **Xceed "Community License Agreement (for Non-Commercial Use)"** since 8.0 (verified from the repo `LICENSE`); commercial use requires a paid license → avoid even for OSS to keep contributor terms simple. 7.x stays Apache-2.0 but is frozen. |
| **AwesomeAssertions** | **9.5.0** | 2026-07-19 | Apache-2.0 fork of the FluentAssertions 8 API; drop-in. **Recommended.** |
| Shouldly | 4.3.0 (5.0.0-preview.2) | 2025-01-23 | BSD-3; fine alternative. |
| **NSubstitute** | **6.2.0** | 2026-08-11 | BSD-3; **recommended**. + NSubstitute.Analyzers.CSharp 1.0.17 (2024-02-11). |
| Moq | 4.20.72 | 2024-09-07 | SponsorLink incident (Aug 2023, 4.20.0) damaged trust; slow cadence. Not recommended. |
| FakeItEasy | 9.0.1 | 2026-01-24 | Fine alternative. |
| Verify.XunitV3 (+ Verify.DiffPlex 3.3.1) | 31.28.0 (32.0.0-beta.8) | 2026-07-31 | Snapshot testing (JSON reports, plans, CLI output). |
| Bogus | 35.6.5 | 2025-10-26 | Test data (optional). |

Prefer hand-written fakes for the Core abstractions (`IRegistry`, `IServiceControl`, `IScheduledTasks`, …) over mocks;
use NSubstitute for incidental collaborators.

### E.3 Coverage, mutation, UI automation, PowerShell

| Tool | Latest | Published | Notes |
|---|---|---|---|
| coverlet.collector / coverlet.msbuild | 10.0.1 | 2026-05-18 | VSTest data collector; `coverlet.msbuild` gives `/p:Threshold=80 /p:ThresholdType=line,branch /p:ThresholdStat=total` gating in either mode. |
| Microsoft.Testing.Extensions.CodeCoverage / dotnet-coverage | 18.10.0 | 2026-08-12 | MS code coverage for MTP; `dotnet-coverage merge` for multi-project. |
| ReportGenerator (dotnet-reportgenerator-globaltool) | 5.5.11 | 2026-07-27 | HTML / Cobertura / MarkdownSummaryGithub; has a `minimumCoverageThresholds` setting to fail the run **(setting name from ReportGenerator docs — background knowledge, verify)**. |
| dotnet-stryker | 4.16.0 | 2026-07-03 | Mutation testing; run on Core only, nightly, `--break-at 70`. |
| FlaUI.Core / FlaUI.UIA3 | 5.0.0 | 2025-02-25 | TFM net8.0-windows (runs on net10). Drives WPF and WinUI 3 (both expose UIA). Needs an interactive desktop session — hosted runners provide one; UIA from a non-elevated test process against an elevated GUI fails (UIPI) → run the tests elevated (hosted runners are admin). |
| BenchmarkDotNet | 0.15.8 | 2025-11-30 | Not needed. |
| PSScriptAnalyzer | 1.25.0 | 2026-03-20 | For repo `.ps1`; Pester 5.x for script tests (PowerShell Gallery; version not re-verified). |
| Microsoft.Extensions.TimeProvider.Testing | 10.9.0 | 2026-08-11 | `FakeTimeProvider` for scheduling logic. |

### E.4 Running tests that touch the registry / need admin

- Unit tests: never touch the real OS — the ADR's `IRegistry` / `IServices` / … abstractions with in-memory fakes.
- Integration tests (Windows project): (a) redirect to `HKCU\Software\OpenTheWindows\Tests\<guid>` and delete in
  `Dispose`; (b) `RegistryKey.CreateSubKey(name, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.Volatile)`
  for keys that vanish on reboot; (c) gate HKLM / services / tasks tests with a skippable-fact attribute that checks
  `IsElevated && Environment.GetEnvironmentVariable("OTW_INTEGRATION") == "1"`; (d) apply / revert round-trips only in a
  **VM** (Windows 11 22H2 / 23H2 / 24H2 lab images).
- GitHub-hosted runners (docs.github.com/actions/reference/runners/github-hosted-runners): "Windows virtual machines are
  configured to run as administrators with User Account Control (UAC) disabled" — so HKLM / service tests **can** run on
  `windows-latest` (4 vCPU / 16 GB / 14 GB SSD for public repos). Nested virtualization: "technically possible while
  using runners, it is not officially supported"; the windows-2025 image lists only WSL (v1 + v2 2.7.11) under
  "Windows features" — Hyper-V is not enabled and Windows Sandbox is a client-SKU feature not present on Windows Server
  2025 images. So: no Hyper-V VMs / Windows Sandbox in hosted CI; run destructive apply / revert suites on a self-hosted
  Windows 11 VM runner (or `windows-11-arm` for ARM smoke tests).
- Apply-mode integration tests on the hosted runner itself only in a throw-away job (the VM is discarded, but mutations
  can break later steps of the same job).

---

## F. Quality gates for C#

### F.1 MSBuild properties (all verified as valid SDK properties; the repo's `Directory.Build.props` already sets most)

| Property | Value | Notes |
|---|---|---|
| `TreatWarningsAsErrors` | `true` | Also `WarningsAsErrors` add `nullable` (repo does). |
| `WarningLevel` | `9999` | Turns on every compiler warning wave; fine. |
| `AnalysisLevel` / `AnalysisMode` | `latest-all` / `All` | Enables every CA rule (NetAnalyzers ships in SDK 10.0.400 = package 10.0.400). Severities via `.editorconfig` `dotnet_diagnostic.CAxxxx.severity`. |
| `EnforceCodeStyleInBuild` | `true` | Makes IDE0xxx rules build-time. IDE0005 (unused using) additionally needs `GenerateDocumentationFile=true` (repo does). |
| `CodeAnalysisTreatWarningsAsErrors` | `true` | |
| `EnforceExtendedAnalyzerRules` | `true` | Only meaningful for analyzer projects; harmless. |
| `Nullable` | `enable` | |
| `GenerateDocumentationFile` | `true` + `NoWarn CS1591` | As the repo does; alternatively require XML docs on public API of Core only. |
| Dead code | `.editorconfig`: `dotnet_diagnostic.IDE0051.severity = error` (unused private member), IDE0052 (unread private member), IDE0060 (unused parameter), IDE0005 (unnecessary using), IDE0079 (unnecessary suppression), CA1812 (uninstantiated internal class), CA1822 (mark static) | Complement with **ReferenceTrimmer 3.5.9** (unused `PackageReference`/`ProjectReference` → warnings RT0001/RT0002) and `dotnet-outdated-tool 4.8.1` (weekly). |
| `Deterministic` / `ContinuousIntegrationBuild` | `true` / `true` when `CI` | Reproducible builds; `DotNet.ReproducibleBuilds 2.0.5` (2026-06-18) sets these plus `EmbedUntrackedSources`, `DebugType embedded` in one package. Source Link for GitHub is **built into the .NET 8+ SDK** (no `Microsoft.SourceLink.GitHub` reference needed; package 10.0.400 exists if you want to pin). |
| `PublishTrimmed` / `PublishAot` | CLI: `PublishAot=true` optional (System.CommandLine 2.0 and CsWin32 are AOT-safe; System.Management / TaskScheduler / WUApi COM are not → keep those behind the Windows project and mark `IsAotCompatible=false` there, or accept IL-only for the CLI). WPF App: neither. | Set `<IsTrimmable>true</IsTrimmable>` + `<IsAotCompatible>true</IsAotCompatible>` on **Core** so trim analyzers (IL2xxx / IL3xxx) run in CI. |
| Platform compat | `CA1416` fires automatically for TFM `net10.0-windows10.0.26100.0`; set `<SupportedOSPlatformVersion>10.0.22000.0</SupportedOSPlatformVersion>` and annotate Core abstractions with `[SupportedOSPlatform("windows")]` where they wrap Windows-only concepts. | |

### F.2 Analyzer packages (nuget.org, 2026-08-16)

| Package | Latest | Published | Recommendation |
|---|---|---|---|
| Microsoft.CodeAnalysis.NetAnalyzers | 10.0.400 (in SDK) | 2026-08-11 | Don't reference explicitly; comes with SDK. |
| Roslynator.Analyzers (+ Roslynator.Formatting.Analyzers, Roslynator.CodeAnalysis.Analyzers) | 4.16.1 | 2026-08-16 | Yes (Apache-2.0). |
| SonarAnalyzer.CSharp | 10.32.0.713 | 2026-08-10 | Yes (LGPL-3.0; analyzer only, no runtime linkage). |
| Meziantou.Analyzer | 3.0.159 | 2026-08-16 | Yes (MIT). MA0004 (ConfigureAwait) overlaps CA2007 — pick one. |
| Microsoft.VisualStudio.Threading.Analyzers | 18.7.23 | 2026-06-22 | Yes for the App/UI project (VSTHRD rules; async void, JTF-style). |
| AsyncFixer | 2.1.0 | 2025-12-29 | Optional (small, MIT). |
| IDisposableAnalyzers | 4.0.8 | 2024-06-19 | Yes (MIT); stale but works. |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.6.0 | 2026-07-02 | Yes — ban `Registry.*` static helpers, `Process.Start` without `ProcessStartInfo`, `Thread.Sleep`, `DateTime.Now`, `Environment.Exit` outside CLI, etc. via `BannedSymbols.txt`. |
| Microsoft.CodeAnalysis.PublicApiAnalyzers | 5.6.0 | 2026-07-02 | Yes for Core (`PublicAPI.Shipped.txt` / `Unshipped.txt`) if Core is ever consumed as a package; otherwise skip. |
| StyleCop.Analyzers | 1.2.0-beta.556 (last stable 1.1.118, 2019) | 2023-12-20 | Effectively unmaintained (no release since Dec 2023) — **skip**; Roslynator + IDE0055 formatting + `dotnet format` cover style. |
| ErrorProne.NET.CoreAnalyzers | 0.9.0-beta.4 (stable 0.1.2 from 2018) | 2026-06-05 | Beta-only; optional. |
| SecurityCodeScan.VS2019 | 5.6.7 | 2022-09-05 | **Dead** (no release in 4 years) — skip. |
| Puma.Security.Rules | 2.4.11 | 2022-02-01 | **Dead** — skip. Security static analysis: SonarAnalyzer S-rules + semgrep (`p/csharp`) + CodeQL (`github/codeql-action`, C# supported) instead. |
| xunit.analyzers | 2.0.0 | 2026-08-15 | Yes (comes with xunit.v3). |
| NSubstitute.Analyzers.CSharp | 1.0.17 | 2024-02-11 | Yes. |
| Microsoft.CodeAnalysis.CSharp (Roslyn) | 5.6.0 | 2026-07-02 | Only if writing analyzers/generators. |

### F.3 Formatting

- `dotnet format` is **in-box** in the SDK (`dotnet format --verify-no-changes --severity warn` in CI; `dotnet format style` /
  `dotnet format analyzers` for IDE/CA rules). The NuGet tool `dotnet-format 5.1.250801` (2021) is the legacy stand-alone
  and must not be installed. Alternative: CSharpier 1.3.0 (2026-06-07; opinionated, `CSharpier.MsBuild` 1.3.0 fails the
  build on unformatted files) — choose one, not both.

### F.4 NuGet audit / lock files / SBOM

- Defaults (learn.microsoft.com/nuget/concepts/auditing-packages, updated 2026-07-07): `NuGetAudit=true`;
  `NuGetAuditLevel=low`; **`NuGetAuditMode` defaults to `all` when a project targets net10.0 or higher** (else `direct`).
  Warnings NU1900 (source error), NU1901–NU1904 (low…critical), NU1905 (audit source provides no vulnerability DB).
- Gate: the docs' "dedicated auditing pipeline" pattern —
  `<WarningsAsErrors Condition="'$(AuditPipeline)'=='true'">$(WarningsAsErrors);NU1900;NU1901;NU1902;NU1903;NU1904;NU1905</WarningsAsErrors>`
  and `dotnet restore -p:AuditPipeline=true` in CI; or simply keep `TreatWarningsAsErrors=true` (repo does) so any NU19xx
  fails restore. Optionally assert `RestoreProjectsAuditedCount == RestoreProjectCount` in `Directory.Solution.targets`.
- **`dotnet package list --vulnerable --include-transitive --format json`** (verb-noun form since .NET 10; `dotnet list
  package` still works) never returns non-zero for findings — NuGet/Home#11315 closed **not planned** (2024-10-14);
  dotnet/sdk#38994 redirected there. Use it only to produce a report; the build gate is restore-time audit.
- **Fix needed in the repo:** `NuGet.config` `<auditSources>` currently points to
  `https://api.nuget.org/v3/vulnerabilities/index.json`. Per the docs an audit source is a **service index** —
  `https://api.nuget.org/v3/index.json` or the vulnerability-only index `https://data.nuget.org/v3/index.json`.
  A non-service-index URL yields NU1905 / no audit. Change it to `https://data.nuget.org/v3/index.json`.
- Central Package Management (`Directory.Packages.props`, `ManagePackageVersionsCentrally=true`,
  `CentralPackageTransitivePinningEnabled=true`) + `RestorePackagesWithLockFile=true` + `dotnet restore --locked-mode`
  in CI (repo has `RestoreLockedMode` when `CI=true`) — with lock files, consider `global.json rollForward: disable`
  (docs recommendation) or accept `latestPatch`.
- SBOM: **Microsoft.Sbom.DotNetTool 4.1.5** (2025-12-15; SPDX 2.2/3.0, `sbom-tool generate`) or **CycloneDX 6.2.0**
  (2026-04-27; `dotnet CycloneDX <sln> -o sbom --json`). Recommend CycloneDX (JSON, widely ingested) attached to each
  release plus the GitHub build-provenance attestation (§G.6).

### F.5 Repo-level tools (versions verified 2026-08-16)

| Tool | Version | Source | Install on Windows | Notes |
|---|---|---|---|---|
| gitleaks | 8.30.1 | GitHub release 2026-03-21; winget `Gitleaks.Gitleaks` 8.30.1 | `winget install Gitleaks.Gitleaks` | MIT. `gitleaks git --redact --exit-code 1` (v8.19+ subcommands). |
| semgrep | 1.173.0 | GitHub 2026-08-13; PyPI wheel `semgrep-1.173.0-...-win_amd64.whl`, classifier "Operating System :: Microsoft :: Windows"; docs list "macOS, Linux, Windows users – using pipx" | `pipx install semgrep` (or `uv tool install semgrep`) | LGPL-2.1 CLI; native Windows is now supported (no WSL needed). Not in winget. Rulesets `p/csharp`, `p/security-audit`, `p/secrets`. |
| opengrep | 1.27.1 | GitHub 2026-08-14 | binary release | LGPL-2.1 fork; alternative if Semgrep licensing/telemetry is a concern. |
| jscpd | 5.0.15 | npm | `npm i -D jscpd@5.0.15` (repo pins) | copy-paste detection. |
| pre-commit | 4.6.2 | PyPI 2026-08-10 | `pipx install pre-commit` | Works on Windows (Python-based hooks). Alternative **Husky.Net 0.9.1** (dotnet tool `husky`, 2026-03-13) or plain `git config core.hooksPath .githooks` with PowerShell scripts — recommended (no Python dependency for contributors). |
| PSScriptAnalyzer | 1.25.0 | GitHub 2026-03-20 | `Install-PSResource PSScriptAnalyzer` | Repo already has `PSScriptAnalyzerSettings.psd1`. |
| markdownlint-cli2 | 0.23.2 | npm | repo pins | |
| cspell | 10.0.1 | npm / GitHub 2026-05-31 | `npm i -D cspell` | |
| editorconfig-checker | 3.11.1 | GitHub 2026-08-08 (winget `EditorConfig-Checker.EditorConfig-Checker` lags at 3.8.0) | binary / `npm i -D editorconfig-checker` | |
| actionlint | 1.7.12 | GitHub 2026-03-30; winget `rhysd.actionlint` 1.7.12 | `winget install rhysd.actionlint` | Lint GH workflows. |
| ReferenceTrimmer | 3.5.9 | nuget 2026-08-14 | PackageReference (analyzer-style) | Unused references → warnings. |
| dotnet-outdated-tool | 4.8.1 | nuget 2026-06-11 | `dotnet tool install dotnet-outdated-tool` | Weekly job. |
| Nerdbank.GitVersioning / MinVer / GitVersion.Tool | 3.10.91 / 7.0.0 / 6.8.2 | nuget | optional | Repo uses `VersionPrefix` — fine. |
| husky (npm) / lint-staged / @commitlint/cli | 9.1.7 / 17.3.0 / 21.2.2 | npm | optional | Only if a JS hook chain is preferred. |
| CodeQL | github/codeql-action v4 (**not re-verified this session**) | GitHub | workflow | C# supported; free for public repos. |

### F.6 Windows-only gotchas for the gates

- Generated code: exclude `**/*.g.cs`, `**/*.g.i.cs`, `**/*.designer.cs`, `**/XamlTypeInfo.g.cs` from style rules with a
  `[*.g.cs] generated_code = true` section in `.editorconfig` (Roslyn honours `generated_code`) — otherwise
  `TreatWarningsAsErrors` breaks WPF/WinUI builds on IDE0xxx/CA rules in generated files.
- `dotnet format --verify-no-changes` runs the XAML/markup compilers via design-time build; run it after `dotnet restore`
  and pass `--no-restore` in CI.
- CA1416: WPF/WinUI TFM implies Windows, but Core (`net10.0`) calling `OperatingSystem.IsWindows()`-guarded code needs
  `[SupportedOSPlatformGuard]` patterns; keep all Win32 in the Windows project.

### F.7 GitHub Actions (verified 2026-08-16 via GitHub Releases API and actions/runner-images README)

| Item | Value | Notes |
|---|---|---|
| `windows-latest` | **Windows Server 2025** image `windows-2025-vs2026` (labels `windows-latest`, `windows-2025`, `windows-2025-vs2026`) | Image 20260810: OS 10.0.26100 build 33158; **VS Enterprise 2026 18.8.12023**; Windows SDK **10.0.26100** only; .NET SDKs 8.0.129/206/319/423, 9.0.119/205/316, **10.0.110/204/302**; PowerShell 7.6.4; WiX 3.14 (v3 only); WSL2 enabled; Hyper-V not enabled. |
| `windows-2022` | still available | VS Enterprise 2022 17.14.37516; same SDK set. Use for a "VS 2022 build still works" job only. |
| `windows-11-arm` | available (Arm64) | Smoke-test ARM64 publish. |
| actions/checkout | v7.0.1 (2026-07-20) | |
| actions/setup-dotnet | **v6.0.0** (2026-07-16) | `with: global-json-file: global.json` (installs 10.0.400); `cache: true` + `cache-dependency-path: '**/packages.lock.json'`. |
| actions/cache | v6.1.0 (2026-06-26) | Manual NuGet cache key on lock files if not using setup-dotnet cache. |
| actions/upload-artifact / download-artifact | v7.0.1 / v8.0.1 | |
| actions/attest-build-provenance | **v4.2.2** (2026-08-06) | See §G.6. |
| Runner privileges | Admin, UAC disabled (docs) | Enables HKLM integration tests. |

Cache NuGet: `~/.nuget/packages` keyed on `hashFiles('**/packages.lock.json')`; set `NUGET_PACKAGES` to a workspace
path on Windows to avoid the C: drive filling.

---

## G. Packaging and distribution without code signing

### G.1 WiX Toolset

| Item | Value | Source |
|---|---|---|
| Latest | **v7.0.0** (2026-04-06) | GitHub wixtoolset/wix release; nuget `wix` (dotnet tool) 7.0.0, `WixToolset.Sdk` 7.0.0, `WixToolset.UI.wixext` 7.0.0, `WixToolset.Util.wixext` 7.0.0; winget `WiXToolset.WiXCLI` 7.0.0.0 |
| License | **Microsoft Reciprocal License (MS-RL)** — `LICENSE.TXT` in wixtoolset/wix: "This software is released under the Microsoft Reciprocal License (MS-RL)". OSI-approved; file-level reciprocity applies to modifications of WiX itself, not to MSIs you build. **Not MIT.** | GitHub |
| Heat | `WixToolset.Heat` last 6.0.2 (2025-08-28) — harvesting is deprecated since v5; use `<Files Include="publish\**" />` in v5+ SDK-style `.wixproj`. | nuget |
| HeatWave | VS extension for WiX v4+ (FireGiant); optional. | (not verified) |
| Usage | `dotnet tool install --global wix --version 7.0.0`; `.wixproj` with `<Project Sdk="WixToolset.Sdk/7.0.0">`; `dotnet build installer/OpenTheWindows.wixproj -c Release -p:Platform=x64` | |

MSI without a signature: Windows Installer runs fine; UAC shows the yellow "Unknown publisher" consent; SmartScreen may
show "Windows protected your PC" for a downloaded MSI/EXE with Mark-of-the-Web until reputation builds (Application
Reputation applies to .exe/.msi downloaded via browsers — background knowledge). Users can "More info → Run anyway".
`winget install` downloads without MotW prompts in practice but validation still requires the AV/PUA scan (G.4).

### G.2 Publishing the CLI

```powershell
dotnet publish src/OpenTheWindows.Cli -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
  -p:DebugType=embedded -p:PublishReadyToRun=true
```

Trimming (`PublishTrimmed=true`) or `PublishAot=true` only if the Windows project stays trim-clean (System.Management,
TaskScheduler and WUApi COM interop are not) — realistic plan: framework-dependent single-file for portable + MSI, plus a
self-contained single-file for the "no runtime installed" case; keep AOT as a stretch goal for a `scan`-only tool.

### G.3 Publishing WinUI 3 unpackaged (if chosen)

`dotnet publish src/App -c Release -r win-x64 -p:Platform=x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true --self-contained -p:PublishReadyToRun=true`
→ folder output (no single-file). Package that folder with WiX. For WPF: `dotnet publish -c Release -r win-x64 --self-contained -p:PublishReadyToRun=true` (folder or single-file; single-file WPF works but extracts natives — fine).

### G.4 winget (community repo) requirements for unsigned installers (verified from microsoft/winget-pkgs docs, 2026-08-16)

- No code-signing requirement for `exe`/`msi`/`zip`/`portable` installers; only MSIX/APPX need `SignatureSha256`
  (Validation step 09 "MSIX-specific validation"). Every installer needs `InstallerSha256` (`winget hash <file>`).
- Validation pipeline: SHA256 check, static AV scanning with multiple engines, **PUA detection — "a package that is flagged
  as PUA cannot be accepted, regardless of the application's legitimacy"** (a debloat/privacy tool that kills services and
  removes apps must avoid PUA heuristics: no packed/obfuscated binaries, no silent registry mass-writes at install time,
  clear uninstall), URL SmartScreen reputation check, silent install/uninstall must work ("silent with progress"),
  installer URLs must be version-specific (GitHub Release asset URLs are fine), scripts (`.ps1`/`.bat`) are not allowed
  as installers.
- Recommended manifest set: `installers` with `msi` (x64, `Scope: machine`, `InstallerSwitches.Silent: /qn`) and
  `portable` (the CLI `otw.exe`, `Commands: [otw]`, `PortableCommandAlias`) — both are accepted unsigned.
- Manifest tooling: `wingetcreate` (Microsoft.WingetCreate — version not re-verified) or `winget-releaser` GitHub Action.

### G.5 Code signing options for later

- **Azure Artifact Signing (formerly Trusted Signing)** — learn.microsoft.com/azure/artifact-signing/quickstart
  (ms.date 2026-05-21) and pricing page (accessed 2026-08-16): **Basic $9.99/month, 5,000 signatures/month; Premium
  $99.99/month, 100,000 signatures/month; $0.005/signature beyond quota**; Basic = 1 of each certificate profile type,
  Premium = 10. Eligibility: "Public Trust certificates are available to organizations in the United States, Canada, the
  European Union, the United Kingdom, Australia, New Zealand, Japan, South Korea, Singapore, Switzerland, Norway, and
  Israel. **Individual developers must be located in the United States or Canada.**" Individual identity validation
  uses the Azure billing account identity + AU10TIX/Verified ID; org validation takes "1 to 20 business days". Public
  Trust certs are short-lived (rotated) and signing goes through `signtool` + the dlib, or the
  `azure/artifact-signing-action` — good fit for GitHub Actions later. Note: an unsigned → signed switch changes the
  MSI `UpgradeCode`-independent trust, not the product identity; SmartScreen reputation for Artifact Signing certs is
  said to accrue quickly (background knowledge, not verified).
- OV/EV certificates from a CA (hardware token / cloud HSM required since 2023 CA/B Forum rules) — more expensive.
- Sigstore **cosign v3.1.3** (2026-08-06) — keyless signing of release artifacts / SBOMs; verifiable with `cosign verify-blob`.

### G.6 Compensating control now: GitHub artifact attestations

- `actions/attest-build-provenance@v4` (v4.2.2, 2026-08-06) with `permissions: id-token: write, attestations: write,
  contents: read` and `subject-path: artifacts/**/*.{exe,msi,zip}` produces SLSA provenance signed via Sigstore and stored
  in the repo's attestation store; users verify with `gh attestation verify <file> --repo <owner>/<repo>`.
- Also publish `SHA256SUMS.txt` (+ the CycloneDX SBOM) as release assets and print the expected hashes in the release
  notes; document the `gh attestation verify` step in README/SECURITY.md. This is the honest substitute for Authenticode
  until signing is funded, and it is what enterprises can audit.

---

## H. Gotchas (collected)

1. **WinUI 3 + `TreatWarningsAsErrors` + `latest-all`**: `XamlTypeInfo.g.cs`, `*.g.cs`, `App.g.i.cs` trigger CA/IDE rules
   (CA1812, CA1822, IDE0005, nullable CS86xx). Use `generated_code = true` in `.editorconfig` for `*.g.cs`/`*.g.i.cs`, and
   `<NoWarn>` for `CsWinRT1028/1030` (partial classes) if the AOT analyzer complains. Prior to 1.8.8 XAML compiler errors
   were hidden under `dotnet build` (MSB3073) — pin ≥ 1.8.8 / 2.x.
2. **TFM**: `net10.0-windows10.0.26100.0` is the newest projection available (`Microsoft.Windows.SDK.NET.Ref 10.0.26100.87`).
   `TargetPlatformMinVersion` (WinUI/MSIX) and `SupportedOSPlatformVersion` (analyzer) should both be `10.0.22000.0` for a
   Windows-11-only app; CA1416 then allows Windows 11 APIs unguarded and flags anything newer.
3. **`EnableMsixTooling=true`** in a WinUI project makes `dotnet build` expect the packaging tooling
   (`Microsoft.Windows.SDK.BuildTools.MSIX`) and, in VS, `Package.appxmanifest`; unpackaged builds should set
   `WindowsPackageType=None` and can leave `EnableMsixTooling` on only for the packaged configuration.
4. **WinUI 3 unit tests** need the WinUI test host app (MSTest `[UITestMethod]`, `Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer`)
   — VS Test Explorer works, `dotnet test` is fragile → keep ViewModels in a plain library and test them with xunit; UI
   E2E via FlaUI.
5. **`<COMReference>` is unsupported by `dotnet build`** (MSB4803) — affects WUApiLib, TaskScheduler typelibs, WMI scripting
   typelibs; use managed wrappers / hand-written interfaces (§D.4).
6. **VS 2022 vs SDK 10.0.400** (§A.4) and **dev PATH x86-first** (§A.3).
7. **Windows App SDK lifecycle**: each 2.x "Current" line is serviced ~12 months; 1.8 EOL 2026-09-09; plan a quarterly bump.
8. **CommunityToolkit.WinUI has no net10 asset** (net9.0-windows asset used) and no explicit 2.x support statement; run a
   smoke test before committing to SettingsCard/SettingsExpander on 2.4.0.
9. **FluentAssertions 8 license** (§E.2); **WiX MS-RL** (§G.1); **LGPO not redistributable** (§D.5).
10. **NuGet audit**: `dotnet package list --vulnerable` exit code 0; fix `auditSources` URL (§F.4).
11. **Hosted runners**: admin + UAC off (good for HKLM tests), no Hyper-V/Windows Sandbox (§E.4); `windows-latest` now
    ships VS 2026 and .NET SDK 10.0.302 → always `actions/setup-dotnet` with `global-json-file`.
12. **xunit.v3 4.0.0** is one day old (MTP v2 only). If Test Explorer or coverage tooling lags, pin 3.2.2 for a month.
13. **System.Management / TaskScheduler / WUApi COM are not trim/AOT-safe** — never enable `PublishTrimmed`/`PublishAot`
    on the Windows project; the CLI can be single-file framework-dependent or self-contained without trimming.
14. **Windows SDK 10.0.28000 (Windows 11 26H1)** exists as C++ SDK / BuildTools, but there is no .NET projection yet — do
    not set the TFM to 28000.

---

## Sources

- .NET release metadata: https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json and
  `.../10.0/releases.json`, `.../11.0/releases.json`, `.../9.0/releases.json` (accessed 2026-08-16)
- .NET SDK / MSBuild / Visual Studio versioning: https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs (ms.date 2026-06-08)
- global.json overview: https://learn.microsoft.com/en-us/dotnet/core/tools/global-json (ms.date 2026-03-05)
- dotnet package list: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list (ms.date 2025-10-28)
- NuGet auditing: https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages (updated 2026-07-07)
- NuGet/Home#11315 (exit code, closed not planned): https://github.com/NuGet/Home/issues/11315 ; dotnet/sdk#38994
- Windows App SDK release channels: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels (updated 2026-08-13)
- Windows App SDK 2.x release notes: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-2-0
- Windows App SDK 1.8 release notes: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-1-8
- Windows App SDK 1.6 release notes (Native AOT): https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-1-6
- Windows App SDK 1.1 release notes (elevation): https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-1-1
- Windows App SDK 1.0 release notes: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-1-0
- Deploy unpackaged apps: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps
- WinUI 3 quick start (VS 2026 / dotnet new): https://learn.microsoft.com/en-us/windows/apps/get-started/start-here (ms.date 2026-07-21)
- Template package contents: https://www.nuget.org/packages/Microsoft.WindowsAppSDK.WinUI.CSharp.Templates/0.0.6-alpha (nupkg inspected)
- Windows SDK archive / current SDK (10.0.28000 = Windows 11 26H1): https://developer.microsoft.com/en-us/windows/downloads/sdk-archive/ ; https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/
- WPF what's new .NET 10: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100 (ms.date 2026-02-10)
- NuGet registration API (all package versions/dates/dependencies): https://api.nuget.org/v3/registration5-gz-semver2/{id}/index.json
- xUnit.net v3 4.0.0 release notes: https://xunit.net/releases/v3/4.0.0
- FluentAssertions license: https://github.com/fluentassertions/fluentassertions/blob/main/LICENSE ; AwesomeAssertions: https://github.com/AwesomeAssertions/AwesomeAssertions
- WiX license: https://github.com/wixtoolset/wix/blob/main/LICENSE.TXT ; releases: https://github.com/wixtoolset/wix/releases
- winget-pkgs docs: https://github.com/microsoft/winget-pkgs/blob/master/doc/Validation.md , .../doc/Policies.md , .../doc/Authoring.md
- GitHub-hosted runners reference: https://docs.github.com/en/actions/reference/runners/github-hosted-runners ; https://docs.github.com/en/actions/using-github-hosted-runners/using-github-hosted-runners/about-github-hosted-runners
- Runner images: https://github.com/actions/runner-images/blob/main/README.md ; https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-VS2026-Readme.md
- GitHub releases (via `gh api repos/<owner>/<repo>/releases/latest`): wixtoolset/wix, gitleaks/gitleaks, semgrep/semgrep, opengrep/opengrep, rhysd/actionlint, editorconfig-checker/editorconfig-checker, PowerShell/PSScriptAnalyzer, streetsidesoftware/cspell, pre-commit/pre-commit, actions/setup-dotnet, actions/attest-build-provenance, actions/cache, actions/checkout, actions/upload-artifact, actions/download-artifact, microsoft/WindowsAppSDK, dotMorten/WinUIEx, CommunityToolkit/Windows, CommunityToolkit/dotnet, lepoco/wpfui, FlaUI/FlaUI, xunit/xunit, microsoft/CsWin32, dahall/TaskScheduler, Fleex255/PolicyPlus, microsoft/testfx, stryker-mutator/stryker-net, microsoft/sbom-tool, CycloneDX/cyclonedx-dotnet, sigstore/cosign, alirezanet/Husky.Net, microsoft/winget-cli, dotnet/command-line-api, Cysharp/ConsoleAppFramework, Tyrrrz/CliFx, microsoft/CsWinRT, kucherenko/jscpd, dfederm/ReferenceTrimmer, PowerShell/PowerShell
- dotnet/msbuild#3986 (COMReference unsupported in dotnet build): https://github.com/dotnet/msbuild/issues/3986
- Semgrep CLI install (Windows via pipx): https://docs.semgrep.dev/getting-started/cli ; PyPI: https://pypi.org/project/semgrep/
- npm registry (`npm view`): jscpd, markdownlint-cli2, cspell, husky, lint-staged, @commitlint/cli ; PyPI: pre-commit, pipx
- winget (`winget show --exact`): Microsoft.DotNet.SDK.10, Microsoft.DotNet.DesktopRuntime.10, Microsoft.WindowsAppRuntime.2 / .2.0 / .1.8, Gitleaks.Gitleaks, rhysd.actionlint, WiXToolset.WiXCLI, Microsoft.WindowsSDK.10.0.26100 / .28000, EditorConfig-Checker.EditorConfig-Checker
- Azure Artifact Signing: https://learn.microsoft.com/en-us/azure/artifact-signing/overview ; https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart (ms.date 2026-05-21) ; pricing https://azure.microsoft.com/en-us/pricing/details/artifact-signing/
- Registry Policy File Format: https://learn.microsoft.com/en-us/previous-versions/windows/desktop/policy/registry-policy-file-format
- IGroupPolicyObject: https://learn.microsoft.com/en-us/windows/win32/api/gpedit/nn-gpedit-igrouppolicyobject
- Security Compliance Toolkit (LGPO): https://learn.microsoft.com/en-us/windows/security/operating-system-security/device-management/windows-security-configuration-framework/security-compliance-toolkit-10
- NetGetJoinInformation: https://learn.microsoft.com/en-us/windows/win32/api/lmjoin/nf-lmjoin-netgetjoininformation ; IsDeviceRegisteredWithManagement: https://learn.microsoft.com/en-us/windows/win32/api/mdmregistration/nf-mdmregistration-isdeviceregisteredwithmanagement ; dsregcmd: https://learn.microsoft.com/en-us/entra/identity/devices/troubleshoot-device-dsregcmd
- Local machine checks: `dotnet --list-sdks` / `--list-runtimes` / `--info` on both hosts; `C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.2\` directory listing; `C:\Program Files\dotnet\sdk\9.0.317\Microsoft.NETCoreSdk.BundledVersions.props`
