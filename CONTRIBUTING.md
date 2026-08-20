# Contributing

Thanks for helping make Windows 11 quieter and safer. This project holds itself
to a high bar because it runs elevated on other people's machines. `README.md`
is the product/user-facing document; everything a developer needs is here.

## Requirements (to build)

- .NET SDK 10.0.400 (pinned in `global.json`), Node 22 (repo tooling only),
  PowerShell 7.4+, Git.
- Optional for the full gate set: Python 3.9+ (semgrep) and gitleaks
  (`winget install Gitleaks.Gitleaks`).
- Visual Studio 2022 cannot load SDK 10.0.400 solutions; use the CLI, VS Code
  or Visual Studio 2026.

## Build and run

```powershell
git clone https://github.com/RandyNorthrup/open-the-windows.git
cd open-the-windows
npm ci                                  # repo tooling (jscpd, markdownlint)
dotnet tool restore                     # reportgenerator, CycloneDX
dotnet restore OpenTheWindows.slnx
dotnet build OpenTheWindows.slnx -c Release
dotnet test OpenTheWindows.slnx -c Release --no-build
```

Run the CLI and the GUI from the build output:

```powershell
artifacts\bin\OpenTheWindows.Cli\release\otw.exe doctor
artifacts\bin\OpenTheWindows.App\release\OpenTheWindows.exe   # elevates (UAC)
```

Publish a single-file trimmed CLI plus the self-contained app, with an SBOM and
hashes:

```powershell
pwsh build/publish.ps1 -Runtime win-x64        # or omit -Runtime for x64 + arm64
```

## Architecture

.NET 10 (LTS) · WPF (Fluent) · System.CommandLine 2.0 · xunit.v3 on
Microsoft.Testing.Platform · Windows 11 21H2+ (x64, ARM64).

Layering: `OpenTheWindows.Core` (pure .NET, all logic) → `OpenTheWindows.Windows`
(the only project that touches the OS) → `OpenTheWindows.Cli` / `OpenTheWindows.App`.

## Ground rules

1. **Gates are the contract.** `pwsh build/quality.ps1` must pass before you
   open a pull request; `pwsh build/quality.ps1 -Ci` runs them in locked-restore mode.
2. **No placeholder code.** No mock data outside tests, no silent fallbacks, no
   "TODO: implement". A function that cannot do its job throws or returns an
   explicit error.
3. **No magic literals.** Named constants, enums or configuration; idiomatic
   `0`, `1`, `-1`, empty collections and booleans in direct conditions are fine.
4. **No dead code.** Unused members, files, exports, packages and stale config
   are build errors; delete rather than comment out.
5. **Never suppress without asking.** Fix the finding. Do not add a
   `[SuppressMessage]`, `#pragma warning`, `NoWarn`, or `.editorconfig` severity
   change without explicit maintainer approval, ever.
6. **Never disable functionality to pass.** Do not remove or weaken an existing
   feature to make a build, test or gate go green; fix the root cause.
7. **Every tweak is data.** New tweaks are catalogue entries with sources,
   revert path, risk, level, `appliesTo` and `verifiedOn` — never ad-hoc code.
   New *action kinds* are code and need tests.
8. **Prove gates and tests can fail.** A new test must have a case that would
   fail without the change; a new gate must be shown failing on a broken input.
9. **Docs stay honest.** README commands must have been run; `CHANGELOG.md` gets
   an entry for every meaningful change.

## Quality gates

One command runs everything and fails on any blocking issue:

```powershell
pwsh build/quality.ps1            # all gates
pwsh build/quality.ps1 -Ci        # locked restore (what CI runs)
pwsh build/quality.ps1 -Only test # a single gate
```

| Gate | Command | What makes it a gate |
| ------ | --------- | ---------------------- |
| restore | `dotnet restore OpenTheWindows.slnx [--locked-mode]` | `NuGetAudit` (all, low) + `TreatWarningsAsErrors` → vulnerable/deprecated packages fail restore |
| format | `dotnet format OpenTheWindows.slnx --verify-no-changes` | exit 2 on any whitespace/style drift |
| build | `dotnet build OpenTheWindows.slnx -c Release` | `TreatWarningsAsErrors`, `AnalysisLevel latest-all`, `EnforceCodeStyleInBuild`, Roslynator + Sonar + Meziantou + IDisposable + VS-Threading analyzers, banned APIs (`BannedSymbols.txt`), unused references (ReferenceTrimmer), dead code (IDE0051/52/59/60/005) |
| test | `dotnet test OpenTheWindows.slnx -c Release --coverage --coverage-output-format cobertura --results-directory artifacts/coverage` | xunit.v3 on Microsoft.Testing.Platform |
| coverage | `dotnet reportgenerator … minimumCoverageThresholds:lineCoverage=90 minimumCoverageThresholds:branchCoverage=80` | exit 1 below threshold (bare token syntax; `-settings:` forms are silently ignored) |
| catalog | built `otw.exe catalog validate` on the embedded catalogue | non-zero fails; proven to reject `tests/fixtures/catalog-bad` (exit 4) so it is not decorative |
| secrets | `gitleaks git … && gitleaks dir …` with `.gitleaks.toml` | `--exit-code 1`; history + working tree; custom rules for VM-inventory passwords and OpenSSH keys |
| sast | `semgrep scan --config p/csharp --config p/secrets --config p/github-actions --error` | exit 1 on findings |
| duplication | `npx jscpd --config .jscpd.json .` | `threshold: 0`, `exitCode: 1` |
| markdown | `npx markdownlint-cli2` | default rules, 120-col |
| powershell | `Invoke-ScriptAnalyzer -Path build -Recurse -Settings PSScriptAnalyzerSettings.psd1` | runner fails on **any** record (not `-EnableExit`, which a wrapper can mask) |

Every gate above has been observed to fail on a deliberately broken input before
being trusted.

## Environment variables

The product reads none. Developer helpers read `OTW_VM_HOST`, `OTW_VM_USER` and
`OTW_VM_KEY` for the lab VM (see [.env.example](.env.example) and `build/vm.ps1`).

## Project structure

```text
OpenTheWindows.slnx          solution (slnx)
Directory.Build.props        compiler + analyzer gates for every project
Directory.Build.targets      exe-only relaxations, BannedSymbols wiring
Directory.Packages.props     ALL package versions (central package management)
global.json                  SDK 10.0.400 + Microsoft.Testing.Platform runner
BannedSymbols.txt            banned APIs for src/ (RS0030)
.editorconfig                style + analyzer severities
src/OpenTheWindows.Core      domain model, engine, catalogue, audit, journal
src/OpenTheWindows.Windows   OS adapters (registry, services, elevation)
src/OpenTheWindows.Cli       otw.exe (System.CommandLine)
src/OpenTheWindows.App       WPF GUI (DesktopApp, MainWindow, view models)
tests/OpenTheWindows.TestSupport   shared fakes
tests/*.Tests                one test project per shipping project
catalog/                     tweak catalogue (JSON) + schema + profiles + baselines
admx/                        Group Policy ADMX/ADML templates
build/                       quality.ps1, publish.ps1, vm.ps1, smoke harnesses
installer/                   WiX MSI project
packaging/                   winget manifests
```

Build output goes to `artifacts/` (gitignored) via `UseArtifactsOutput`.

## Where things live

- Package versions: `Directory.Packages.props` only.
- Analyzer severities: `.editorconfig`; analyzer packages:
  `Directory.Packages.props` (`GlobalPackageReference`).
- Banned APIs: `BannedSymbols.txt`.
- Tool versions: `.config/dotnet-tools.json`, `package.json`,
  `build/requirements-tools.txt`.

## Commit style

Conventional Commits (`feat`, `fix`, `docs`, `test`, `build`, `ci`,
`refactor`, `chore`) with a scope (`core`, `windows`, `cli`, `app`, `catalog`,
`build`).

## Troubleshooting

- **`dotnet --list-sdks` shows nothing / SDK not found**: on some machines the
  x86 host `C:\Program Files (x86)\dotnet` precedes the x64 host on `PATH`.
  Put `C:\Program Files\dotnet\` first (machine PATH) and open a new shell.
  `build/*.ps1` call the x64 host explicitly.
- **`dotnet test` says "Testing with VSTest target is no longer supported"**:
  `global.json` must contain `"test": { "runner": "Microsoft.Testing.Platform" }`
  (it does); do not add `Microsoft.NET.Test.Sdk`.
- **`Invoke-ScriptAnalyzer` throws NullReferenceException**: a non-existent
  compatibility profile name in `PSScriptAnalyzerSettings.psd1`; PSSA 1.25 ships
  `core-6.1.0-windows` as the newest core profile.
- **The GUI does not appear in front**: it runs elevated; unelevated tools
  cannot bring it to the foreground (UIPI). `build/smoke-gui.ps1` self-elevates.
- **SSH key "bad permissions" (Windows OpenSSH)**:
  `icacls <key> /inheritance:r /grant:r "$env:USERNAME:F"`.

## Reporting security issues

See [SECURITY.md](SECURITY.md). Please do not open public issues for
vulnerabilities.
