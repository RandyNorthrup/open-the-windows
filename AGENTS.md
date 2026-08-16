# Agent instructions — Open the Windows

These rules apply to any AI coding agent working in this repository. They
restate the project's standards; the authoritative details are in
`PLAN.md`, `CONTRIBUTING.md`, `docs/adr/` and `docs/research/`.

## Mandatory sequence (enforced by hooks and gates)

1. Read this file and `docs/milestones/README.md` (the runbook).
2. Run `pwsh build/start-milestone.ps1 -Milestone <Mx>` for the milestone you
   are working on (PLAN.md section 8 says which is in progress). It prints the
   milestone specification `docs/milestones/<Mx>-*.md` into your context and
   records an acknowledgement. Until then, the Claude Code PreToolUse hook
   (`.claude/settings.json` -> `build/hooks/pre-edit.ps1`) **blocks** Edit/Write
   under `src/`, `tests/`, `catalog/`, `build/`, `.github/` and the root build
   files. Documentation edits are always allowed.
3. Follow the spec's implementation steps in order; do not invent scope.
4. `pwsh build/quality.ps1` must be green before you start and before you
   finish; the `plan` gate (`build/check-plan.ps1`) fails if PLAN.md, specs,
   certification records and these instruction files disagree.
5. Commits: git pre-commit hook (`pwsh build/setup-dev.ps1` once) runs
   format/build/test/plan and requires a `CHANGELOG.md` change whenever
   `src/`, `tests/`, `catalog/` or `build/` change.
6. A milestone is done only with `docs/certification/<Mx>.md` filled from the
   template in the runbook and PLAN.md updated. Never mark DONE without it.

## What this project is

A free, MIT-licensed, enterprise-grade Windows 11 privacy / Windows Update /
security-hardening / performance / debloat tool. .NET 10, WPF GUI + `otw.exe`
CLI over one Core engine. It runs **elevated** and changes OS state, so
correctness and reversibility are the product.

## Non-negotiables

- Pausing / holding / deferring Windows Update is a **feature** (Settings pause
  keys incl. extended pause at Advanced tier, WUfB policies, per-update hide,
  supervised temporary stop of `wuauserv`/`UsoSvc`/`BITS`/`DoSvc` in the
  hold/repair flow). What is refused: permanently Disabled start types or
  ownership/ACL hacks on `WaaSMedicSvc`/`UsoSvc` (WaaSMedic reverts them and
  they break Store/Defender), disabling Defender, UAC, or protected services
  (`TrustedInstaller`, `SecurityHealthService`, `WinDefend`, `wscsvc`,
  `EventLog`), hosts-blocking Microsoft endpoints, removing Edge/WebView2
  (`docs/research/03`, `05` §7, PLAN.md D21).
- Per-user settings target the **interactive user's** `HKU\<SID>`, never
  `Registry.CurrentUser` (banned API in `src/`).
- Policy keys are written through the Local GPO path and mirrored; managed
  (MDM/GPO) settings are reported, not overridden.
- Every state change is journaled **before** it happens and verified after.
- Tweaks are catalogue **data** with sources, revert path, risk tier, level,
  `appliesTo` and `verifiedOn`; only the closed set of action kinds executes.
- No telemetry, no network access unless the user opts into the update check.

## Code standards (enforced by the build)

- `TreatWarningsAsErrors`, `AnalysisLevel latest-all`, `EnforceCodeStyleInBuild`,
  Roslynator/Sonar/Meziantou/IDisposable/VS-Threading analyzers, banned APIs,
  ReferenceTrimmer, nullable everywhere. Fix the finding; do not suppress it.
  If a suppression is genuinely right: name the rule, state why inline, add a
  row to `PLAN.md` §7.3.
- No magic numbers/strings/timeouts: constants, enums, or schema-validated
  configuration. `0`, `1`, `-1`, empty collections and direct booleans are fine.
- No dead code, commented-out code, unused files/exports/packages.
- No placeholders, mocks or fake implementations in `src/`. A function that
  cannot do its job throws or returns an explicit failure.
- Time via `TimeProvider`; processes only via the guarded command runner.
- One type per file (MA0048); file-scoped namespaces; explicit types unless
  apparent; `Async` suffix; `_camelCase` private fields.
- Windows APIs only in `OpenTheWindows.Windows`. Core stays `net10.0` and
  Windows-free.

## Analyzer expectations (learned; fix these up front instead of suppressing)

- One type per file, file name = type name (MA0048); a type must not share
  its name with its namespace (MA0049/CA1724 — e.g. `TweakCatalog`, not
  `Catalog`; `DesktopApp`, not `App`).
- No type names in identifiers (CA1720): registry types are `Sz`, `ExpandSz`,
  `MultiSz`, not `String`.
- Abstract records with no implementation must be interfaces (S1694).
- `var` when the type is apparent on the right-hand side (IDE0007), explicit
  type otherwise; expression-bodied members where single-line.
- Prefer concrete collection types for locals/parameters when the analyzer
  asks (CA1859); `[.. x]` collection expressions (IDE0300/IDE0305).
- Dispose struct enumerators explicitly (`using var e = element.EnumerateArray()`),
  IDISP004 flags `foreach` over `JsonElement.EnumerateArray()`.
- `Assert.Contains(x, collection, StringComparer.Ordinal)` for strings (MA0002);
  `string.Create(CultureInfo.InvariantCulture, $"...")` for interpolated output.
- Executable projects: types `internal` (CA1515); WPF classes need
  `x:ClassModifier="internal"` in XAML.
- ReferenceTrimmer (RT0002/RT0003) fails on unused ProjectReference/PackageReference;
  remove them instead of "using" something to silence it.
- Banned: `Registry.CurrentUser`, `DateTime.Now/UtcNow`, `Environment.Exit`,
  `System.Diagnostics.Process` in `src/` (RS0030 with the reason text).
- STJ: source-generated contexts only; `UnmappedMemberHandling = Disallow` for
  catalogue/profile/journal files; polymorphism via `[JsonPolymorphic]` on the
  interface with `FailSerialization` for unknown kinds.
- XML comments in .props/.csproj/.targets must not contain `--`.
- ReportGenerator thresholds: bare `minimumCoverageThresholds:lineCoverage=NN`
  tokens (other forms are silently ignored).
- Tests: xunit.v3 on Microsoft.Testing.Platform (`global.json`); no
  `Microsoft.NET.Test.Sdk`; coverage via `dotnet test --coverage`.

## Testing rules

- Unit tests use the fakes in `tests/OpenTheWindows.TestSupport`; no OS calls.
- Windows integration tests read real OS state only when safe; anything that
  writes runs on the lab VM behind `OTW_INTEGRATION=1` and elevation.
- Every test has a case that would fail without the behaviour; assertions
  target the behaviour, not a downstream symptom.
- Coverage thresholds (line 85 / branch 75, rising) are gates; do not lower them.

## Quality gates

Run `pwsh build/quality.ps1` before finishing any task. It must be green. Gates:
restore (NuGetAudit), format, build, test, coverage, secrets (gitleaks), sast
(semgrep), duplication (jscpd), markdown, powershell (PSScriptAnalyzer). New
gates must be shown to fail on a broken input and logged in `PLAN.md` §7.2.

## Documentation, changelog, planning discipline

- `README.md` only contains commands that have been run successfully.
- `CHANGELOG.md` (Keep a Changelog) gets an entry for every meaningful change.
- `PLAN.md` holds decisions, open questions, escape hatches, milestone status;
  a milestone is complete only when its certification checklist passes.
- Shaping decisions get an ADR in `docs/adr/`.
- Package versions are pinned in `Directory.Packages.props`; verify
  compatibility (peer/transitive) before adding anything, and record the
  source in `PLAN.md` §7.1. Nothing is added without purpose.

## Security rules

- Secrets never enter the repo (`temp/` and `.env` are gitignored; gitleaks
  runs on history and tree). Never print credentials in logs or tool output.
- Do not modify global user memory, machine-wide agent instructions, or IDE
  settings; project-local files only.
- Do not add global installs unless documented in `README.md`.

## Environment notes for agents

- Use `C:\Program Files\dotnet\dotnet.exe` (x64) if `dotnet` resolves to the
  x86 host; `build/*.ps1` already do.
- Tests run on Microsoft.Testing.Platform (`global.json`); do not add
  `Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio`.
- Bash heredocs in this environment mangle non-ASCII and backslashes; write
  files with dedicated file tools.
